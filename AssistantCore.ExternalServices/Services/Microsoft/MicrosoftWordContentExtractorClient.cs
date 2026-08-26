using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftWordContentExtractorClient
{
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace Word = WordNamespace;
    private static readonly byte[] CompoundDocumentSignature =
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/octet-stream",
        "application/zip",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-word.document.macroenabled.12"
    };

    public async Task<MicrosoftWordExtractionResult> ExtractAsync(
        string fileName,
        string? mimeType,
        Stream content,
        long? contentLength,
        long maximumFileSizeBytes,
        long maximumExpandedSizeBytes,
        int maximumExtractedCharacters,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        if (maximumFileSizeBytes <= 0
            || maximumExpandedSizeBytes <= 0
            || maximumExtractedCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFileSizeBytes),
                "Extraction limits must be greater than zero.");
        }

        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".docm", StringComparison.OrdinalIgnoreCase))
        {
            return Result(MicrosoftWordExtractionStatus.UnsupportedFormat);
        }

        if (!string.IsNullOrWhiteSpace(mimeType)
            && !SupportedMimeTypes.Contains(mimeType.Split(';', 2)[0].Trim()))
        {
            return Result(MicrosoftWordExtractionStatus.UnsupportedFormat);
        }

        if (contentLength is < 0 || contentLength > maximumFileSizeBytes)
        {
            return Result(MicrosoftWordExtractionStatus.TooLarge);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var packageStream = await CopyWithinLimitAsync(
            content,
            maximumFileSizeBytes,
            cancellationToken);
        if (packageStream is null)
        {
            return Result(MicrosoftWordExtractionStatus.TooLarge);
        }

        if (HasSignature(packageStream, CompoundDocumentSignature))
        {
            return Result(MicrosoftWordExtractionStatus.EncryptedDocument);
        }

        if (!HasZipSignature(packageStream))
        {
            return Result(MicrosoftWordExtractionStatus.CorruptedDocument);
        }

        try
        {
            using var package = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);
            if (ExceedsExpandedSizeLimit(package.Entries, maximumExpandedSizeBytes))
            {
                return Result(MicrosoftWordExtractionStatus.TooLarge);
            }

            var documentEntry = FindEntry(package, "word/document.xml");
            if (documentEntry is null)
            {
                return Result(MicrosoftWordExtractionStatus.CorruptedDocument);
            }

            var warnings = DetectWarnings(package);
            var units = new List<MicrosoftWordExtractedUnit>();
            var characterCount = 0;

            foreach (var header in package.Entries
                         .Where(entry => IsWordPart(entry, "header"))
                         .OrderBy(entry => entry.FullName, StringComparer.Ordinal))
            {
                await ExtractPartAsync(
                    header,
                    MicrosoftWordExtractedUnitKind.Header,
                    units,
                    maximumExtractedCharacters,
                    () => characterCount,
                    value => characterCount = value,
                    cancellationToken);
            }

            await ExtractDocumentAsync(
                documentEntry,
                units,
                maximumExtractedCharacters,
                () => characterCount,
                value => characterCount = value,
                cancellationToken);

            foreach (var footer in package.Entries
                         .Where(entry => IsWordPart(entry, "footer"))
                         .OrderBy(entry => entry.FullName, StringComparer.Ordinal))
            {
                await ExtractPartAsync(
                    footer,
                    MicrosoftWordExtractedUnitKind.Footer,
                    units,
                    maximumExtractedCharacters,
                    () => characterCount,
                    value => characterCount = value,
                    cancellationToken);
            }

            return units.Count == 0
                ? new MicrosoftWordExtractionResult(
                    MicrosoftWordExtractionStatus.EmptyDocument,
                    [],
                    warnings)
                : new MicrosoftWordExtractionResult(
                    MicrosoftWordExtractionStatus.Success,
                    units,
                    warnings);
        }
        catch (ExtractionLimitExceededException)
        {
            return Result(MicrosoftWordExtractionStatus.TooLarge);
        }
        catch (InvalidDataException)
        {
            return Result(MicrosoftWordExtractionStatus.CorruptedDocument);
        }
        catch (XmlException)
        {
            return Result(MicrosoftWordExtractionStatus.CorruptedDocument);
        }
    }

    private static async Task<MemoryStream?> CopyWithinLimitAsync(
        Stream source,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var destination = new MemoryStream();
        var buffer = new byte[81920];
        long totalBytes = 0;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                destination.Position = 0;
                return destination;
            }

            totalBytes += bytesRead;
            if (totalBytes > maximumBytes)
            {
                await destination.DisposeAsync();
                return null;
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private static bool HasSignature(Stream content, byte[] signature)
    {
        content.Position = 0;
        Span<byte> actual = stackalloc byte[signature.Length];
        var bytesRead = content.Read(actual);
        content.Position = 0;
        return bytesRead == signature.Length
            && actual.SequenceEqual(signature);
    }

    private static bool HasZipSignature(Stream content)
    {
        content.Position = 0;
        Span<byte> signature = stackalloc byte[4];
        var bytesRead = content.Read(signature);
        content.Position = 0;
        return bytesRead == signature.Length
            && signature[0] == 0x50
            && signature[1] == 0x4B
            && ((signature[2] == 0x03 && signature[3] == 0x04)
                || (signature[2] == 0x05 && signature[3] == 0x06)
                || (signature[2] == 0x07 && signature[3] == 0x08));
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive package, string path) =>
        package.Entries.FirstOrDefault(entry =>
            entry.FullName.Equals(path, StringComparison.OrdinalIgnoreCase));

    private static bool ExceedsExpandedSizeLimit(
        IReadOnlyCollection<ZipArchiveEntry> entries,
        long maximumExpandedSizeBytes)
    {
        long totalSize = 0;
        foreach (var entry in entries)
        {
            if (entry.Length > maximumExpandedSizeBytes - totalSize)
            {
                return true;
            }

            totalSize += entry.Length;
        }

        return false;
    }

    private static IReadOnlyList<MicrosoftWordExtractionWarning> DetectWarnings(
        ZipArchive package)
    {
        var warnings = new HashSet<MicrosoftWordExtractionWarning>();
        if (package.Entries.Any(entry =>
                entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add(MicrosoftWordExtractionWarning.MacroIgnored);
        }

        if (package.Entries.Any(entry =>
                entry.FullName.StartsWith("word/embeddings/", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add(MicrosoftWordExtractionWarning.EmbeddedObjectIgnored);
        }

        if (package.Entries
            .Where(entry => entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            .Any(ContainsExternalRelationship))
        {
            warnings.Add(MicrosoftWordExtractionWarning.ExternalLinkIgnored);
        }

        return warnings.OrderBy(warning => warning).ToArray();
    }

    private static bool ContainsExternalRelationship(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = CreateSecureXmlReader(stream);
        var document = XDocument.Load(reader, LoadOptions.None);
        return document.Descendants().Any(element =>
            string.Equals(
                element.Attribute("TargetMode")?.Value,
                "External",
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWordPart(ZipArchiveEntry entry, string partName) =>
        entry.FullName.StartsWith($"word/{partName}", StringComparison.OrdinalIgnoreCase)
        && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static async Task ExtractPartAsync(
        ZipArchiveEntry entry,
        MicrosoftWordExtractedUnitKind kind,
        List<MicrosoftWordExtractedUnit> units,
        int maximumCharacters,
        Func<int> getCharacterCount,
        Action<int> setCharacterCount,
        CancellationToken cancellationToken)
    {
        var root = await LoadXmlAsync(entry, cancellationToken);
        foreach (var paragraph in root.Descendants(Word + "p"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddUnit(
                units,
                kind,
                GetParagraphText(paragraph),
                entry.FullName,
                maximumCharacters,
                getCharacterCount,
                setCharacterCount);
        }
    }

    private static async Task ExtractDocumentAsync(
        ZipArchiveEntry entry,
        List<MicrosoftWordExtractedUnit> units,
        int maximumCharacters,
        Func<int> getCharacterCount,
        Action<int> setCharacterCount,
        CancellationToken cancellationToken)
    {
        var root = await LoadXmlAsync(entry, cancellationToken);
        var body = root.Element(Word + "body")
            ?? throw new XmlException("The Word document has no body element.");

        foreach (var element in body.Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.Name == Word + "p")
            {
                AddUnit(
                    units,
                    GetParagraphKind(element),
                    GetParagraphText(element),
                    entry.FullName,
                    maximumCharacters,
                    getCharacterCount,
                    setCharacterCount);
            }
            else if (element.Name == Word + "tbl")
            {
                AddTable(
                    element,
                    entry.FullName,
                    units,
                    maximumCharacters,
                    getCharacterCount,
                    setCharacterCount);
            }
        }
    }

    private static async Task<XElement> LoadXmlAsync(
        ZipArchiveEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = entry.Open();
        using var reader = CreateSecureXmlReader(stream);
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        return document.Root ?? throw new XmlException("The Word XML part has no root element.");
    }

    private static XmlReader CreateSecureXmlReader(Stream stream) =>
        XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 100_000_000
        });

    private static MicrosoftWordExtractedUnitKind GetParagraphKind(XElement paragraph)
    {
        var properties = paragraph.Element(Word + "pPr");
        var style = properties?
            .Element(Word + "pStyle")?
            .Attribute(Word + "val")?
            .Value;

        if (!string.IsNullOrWhiteSpace(style)
            && (style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
                || style.StartsWith("Title", StringComparison.OrdinalIgnoreCase)
                || style.StartsWith("Titre", StringComparison.OrdinalIgnoreCase)))
        {
            return MicrosoftWordExtractedUnitKind.Title;
        }

        return properties?.Element(Word + "numPr") is not null
            ? MicrosoftWordExtractedUnitKind.ListItem
            : MicrosoftWordExtractedUnitKind.Paragraph;
    }

    private static string GetParagraphText(XElement paragraph)
    {
        var builder = new StringBuilder();
        foreach (var element in paragraph.Descendants())
        {
            if (element.Name == Word + "t")
            {
                builder.Append(element.Value);
            }
            else if (element.Name == Word + "tab" || element.Name == Word + "br")
            {
                builder.Append(' ');
            }
        }

        return Normalize(builder.ToString());
    }

    private static void AddTable(
        XElement table,
        string sourcePart,
        List<MicrosoftWordExtractedUnit> units,
        int maximumCharacters,
        Func<int> getCharacterCount,
        Action<int> setCharacterCount)
    {
        var rows = table.Elements(Word + "tr")
            .Select(row => row.Elements(Word + "tc")
                .Select(cell => Normalize(string.Join(
                    " ",
                    cell.Descendants(Word + "p").Select(GetParagraphText))))
                .ToArray())
            .Where(row => row.Any(value => value.Length > 0))
            .ToArray();

        if (rows.Length == 0)
        {
            return;
        }

        AddUnit(
            units,
            MicrosoftWordExtractedUnitKind.Table,
            "Tableau",
            sourcePart,
            maximumCharacters,
            getCharacterCount,
            setCharacterCount);

        if (rows.Length == 1)
        {
            AddUnit(
                units,
                MicrosoftWordExtractedUnitKind.Table,
                string.Join(" | ", rows[0]),
                sourcePart,
                maximumCharacters,
                getCharacterCount,
                setCharacterCount);
            return;
        }

        var headers = rows[0];
        foreach (var row in rows.Skip(1))
        {
            var values = row.Select((value, index) =>
                index < headers.Length && headers[index].Length > 0
                    ? $"{headers[index]} : {value}"
                    : value);
            AddUnit(
                units,
                MicrosoftWordExtractedUnitKind.Table,
                string.Join(" | ", values),
                sourcePart,
                maximumCharacters,
                getCharacterCount,
                setCharacterCount);
        }
    }

    private static void AddUnit(
        List<MicrosoftWordExtractedUnit> units,
        MicrosoftWordExtractedUnitKind kind,
        string text,
        string sourcePart,
        int maximumCharacters,
        Func<int> getCharacterCount,
        Action<int> setCharacterCount)
    {
        if (text.Length == 0)
        {
            return;
        }

        var newCharacterCount = checked(getCharacterCount() + text.Length);
        if (newCharacterCount > maximumCharacters)
        {
            throw new ExtractionLimitExceededException();
        }

        setCharacterCount(newCharacterCount);
        units.Add(new MicrosoftWordExtractedUnit(kind, units.Count, text, sourcePart));
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static MicrosoftWordExtractionResult Result(MicrosoftWordExtractionStatus status) =>
        new(status, [], []);

    private sealed class ExtractionLimitExceededException : Exception;
}
