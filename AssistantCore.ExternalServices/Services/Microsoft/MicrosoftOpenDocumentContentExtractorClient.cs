using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftOpenDocumentContentExtractorClient
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    private static readonly Dictionary<string, string> ExpectedMimeTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        [".odp"] = "application/vnd.oasis.opendocument.presentation"
    };

    public async Task<MicrosoftTextBasedExtractionResult> ExtractAsync(
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

        var extension = Path.GetExtension(fileName);
        if (!ExpectedMimeTypeByExtension.TryGetValue(extension, out var expectedMimeType))
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.UnsupportedFormat);
        }

        if (!string.IsNullOrWhiteSpace(mimeType))
        {
            var declaredMimeType = mimeType.Split(';', 2)[0].Trim();
            if (!declaredMimeType.Equals(expectedMimeType, StringComparison.OrdinalIgnoreCase)
                && !declaredMimeType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                && !declaredMimeType.Equals("application/zip", StringComparison.OrdinalIgnoreCase))
            {
                return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.UnsupportedFormat);
            }
        }

        if (contentLength is < 0 || contentLength > maximumFileSizeBytes)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var packageStream = await MicrosoftExtractionStreamHelper.CopyWithinLimitAsync(
            content,
            maximumFileSizeBytes,
            cancellationToken);
        if (packageStream is null)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
        }

        if (!HasZipSignature(packageStream))
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.CorruptedDocument);
        }

        try
        {
            using var package = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);
            if (ExceedsExpandedSizeLimit(package.Entries, maximumExpandedSizeBytes))
            {
                return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
            }

            var mimetypeEntry = package.GetEntry("mimetype");
            if (mimetypeEntry is null
                || !(await ReadEntryTextAsync(mimetypeEntry, cancellationToken))
                    .Trim()
                    .Equals(expectedMimeType, StringComparison.Ordinal))
            {
                // Le type declare par la premiere entree de l'archive ne correspond pas a
                // l'extension : c'est le signal d'une extension falsifiee, pas juste un
                // format inhabituel.
                return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.CorruptedDocument);
            }

            var contentEntry = package.GetEntry("content.xml");
            if (contentEntry is null)
            {
                return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.CorruptedDocument);
            }

            var root = await LoadXmlAsync(contentEntry, cancellationToken);
            var units = new List<MicrosoftTextBasedExtractedUnit>();
            var characterCount = 0;

            switch (extension.ToLowerInvariant())
            {
                case ".odt":
                    ExtractTextDocument(root, fileName, maximumExtractedCharacters, units, ref characterCount);
                    break;
                case ".ods":
                    ExtractSpreadsheet(root, fileName, maximumExtractedCharacters, units, ref characterCount);
                    break;
                case ".odp":
                    ExtractPresentation(root, fileName, maximumExtractedCharacters, units, ref characterCount);
                    break;
            }

            return units.Count == 0
                ? MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.EmptyDocument)
                : new MicrosoftTextBasedExtractionResult(MicrosoftTextBasedExtractionStatus.Success, units);
        }
        catch (ExtractionLimitExceededException)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
        }
        catch (InvalidDataException)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.CorruptedDocument);
        }
        catch (XmlException)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.CorruptedDocument);
        }
    }

    private static void ExtractTextDocument(
        XElement root,
        string sourcePart,
        int maximumCharacters,
        List<MicrosoftTextBasedExtractedUnit> units,
        ref int characterCount)
    {
        var body = root.Descendants(Office + "text").FirstOrDefault();
        if (body is null)
        {
            return;
        }

        foreach (var element in body.Elements())
        {
            if (element.Name == Text + "h")
            {
                AddUnit(units, GetTextValue(element), sourcePart, isTitle: true, maximumCharacters, ref characterCount);
            }
            else if (element.Name == Text + "p")
            {
                AddUnit(units, GetTextValue(element), sourcePart, isTitle: false, maximumCharacters, ref characterCount);
            }
            else if (element.Name == Text + "list")
            {
                foreach (var item in element.Descendants(Text + "list-item"))
                {
                    AddUnit(units, GetTextValue(item), sourcePart, isTitle: false, maximumCharacters, ref characterCount);
                }
            }
            else if (element.Name == Table + "table")
            {
                AddTable(element, sourcePart, maximumCharacters, units, ref characterCount);
            }
        }
    }

    private static void ExtractSpreadsheet(
        XElement root,
        string sourcePart,
        int maximumCharacters,
        List<MicrosoftTextBasedExtractedUnit> units,
        ref int characterCount)
    {
        foreach (var sheet in root.Descendants(Table + "table"))
        {
            var sheetName = (string?)sheet.Attribute(Table + "name") ?? string.Empty;
            var rows = sheet.Elements(Table + "table-row")
                .Select(row => row.Elements(Table + "table-cell")
                    .SelectMany(ExpandRepeatedCell)
                    .Select(GetTextValue)
                    .ToArray())
                .Where(row => row.Any(value => value.Length > 0))
                .ToArray();
            if (rows.Length == 0)
            {
                continue;
            }

            AddUnit(units, $"Feuille : {sheetName}", sourcePart, isTitle: true, maximumCharacters, ref characterCount);

            var headers = rows[0];
            foreach (var row in rows.Skip(1))
            {
                var values = row.Select((value, index) =>
                    index < headers.Length && headers[index].Length > 0
                        ? $"{headers[index]} : {value}"
                        : value);
                AddUnit(units, string.Join(" | ", values), sourcePart, isTitle: false, maximumCharacters, ref characterCount);
            }
        }
    }

    private static IEnumerable<XElement> ExpandRepeatedCell(XElement cell)
    {
        var repeated = (int?)cell.Attribute(Table + "number-columns-repeated") ?? 1;
        const int maximumRepeats = 1000;
        for (var index = 0; index < Math.Min(repeated, maximumRepeats); index++)
        {
            yield return cell;
        }
    }

    private static void ExtractPresentation(
        XElement root,
        string sourcePart,
        int maximumCharacters,
        List<MicrosoftTextBasedExtractedUnit> units,
        ref int characterCount)
    {
        var slideNumber = 0;
        foreach (var page in root.Descendants(Draw + "page"))
        {
            slideNumber++;
            var slideName = (string?)page.Attribute(Draw + "name");
            var label = string.IsNullOrWhiteSpace(slideName)
                ? $"Diapositive {slideNumber}"
                : $"Diapositive {slideNumber} : {slideName}";
            AddUnit(units, label, sourcePart, isTitle: true, maximumCharacters, ref characterCount);

            foreach (var paragraph in page.Descendants(Text + "p"))
            {
                AddUnit(units, GetTextValue(paragraph), sourcePart, isTitle: false, maximumCharacters, ref characterCount);
            }
        }
    }

    private static void AddTable(
        XElement table,
        string sourcePart,
        int maximumCharacters,
        List<MicrosoftTextBasedExtractedUnit> units,
        ref int characterCount)
    {
        var rows = table.Elements(Table + "table-row")
            .Select(row => row.Elements(Table + "table-cell")
                .SelectMany(ExpandRepeatedCell)
                .Select(GetTextValue)
                .ToArray())
            .Where(row => row.Any(value => value.Length > 0))
            .ToArray();
        if (rows.Length == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            AddUnit(units, string.Join(" | ", row), sourcePart, isTitle: false, maximumCharacters, ref characterCount);
        }
    }

    private static string GetTextValue(XElement element) => Normalize(element.Value);

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void AddUnit(
        List<MicrosoftTextBasedExtractedUnit> units,
        string text,
        string sourcePart,
        bool isTitle,
        int maximumCharacters,
        ref int characterCount)
    {
        if (text.Length == 0)
        {
            return;
        }

        var newCharacterCount = checked(characterCount + text.Length);
        if (newCharacterCount > maximumCharacters)
        {
            throw new ExtractionLimitExceededException();
        }

        characterCount = newCharacterCount;
        units.Add(new MicrosoftTextBasedExtractedUnit(units.Count, text, sourcePart, isTitle));
    }

    private static async Task<string> ReadEntryTextAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task<XElement> LoadXmlAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var reader = CreateSecureXmlReader(stream);
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        return document.Root ?? throw new XmlException("The OpenDocument content.xml part has no root element.");
    }

    private static XmlReader CreateSecureXmlReader(Stream stream) =>
        XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 100_000_000
        });

    private static bool HasZipSignature(Stream content)
    {
        content.Position = 0;
        Span<byte> signature = stackalloc byte[4];
        var bytesRead = content.Read(signature);
        content.Position = 0;
        return bytesRead == 4 && signature[0] == 0x50 && signature[1] == 0x4B;
    }

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

    private sealed class ExtractionLimitExceededException : Exception;
}
