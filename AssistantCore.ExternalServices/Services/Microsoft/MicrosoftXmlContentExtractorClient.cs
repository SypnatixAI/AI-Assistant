using System.Xml;
using System.Xml.Linq;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftXmlContentExtractorClient
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xml"
    };

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/xml", "text/xml", "text/plain", "application/octet-stream"
    };

    public async Task<MicrosoftTextBasedExtractionResult> ExtractAsync(
        string fileName,
        string? mimeType,
        Stream content,
        long? contentLength,
        long maximumFileSizeBytes,
        int maximumDepth,
        int maximumExtractedCharacters,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        if (!SupportedExtensions.Contains(Path.GetExtension(fileName)))
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.UnsupportedFormat);
        }

        if (!string.IsNullOrWhiteSpace(mimeType)
            && !SupportedMimeTypes.Contains(mimeType.Split(';', 2)[0].Trim()))
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.UnsupportedFormat);
        }

        if (contentLength is < 0 || contentLength > maximumFileSizeBytes)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var buffer = await MicrosoftExtractionStreamHelper.CopyWithinLimitAsync(
            content,
            maximumFileSizeBytes,
            cancellationToken);
        if (buffer is null)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
        }

        if (buffer.Length == 0)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.EmptyDocument);
        }

        buffer.Position = 0;
        XElement root;
        try
        {
            using var reader = CreateSecureXmlReader(buffer);
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
            root = document.Root ?? throw new XmlException("The XML document has no root element.");
        }
        catch (XmlException)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.CorruptedDocument);
        }

        var units = new List<MicrosoftTextBasedExtractedUnit>();
        var characterCount = 0;
        try
        {
            AppendLeaves(root, root.Name.LocalName, fileName, maximumDepth, 1, maximumExtractedCharacters, units, ref characterCount);
        }
        catch (ExtractionLimitExceededException)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
        }

        return units.Count == 0
            ? MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.EmptyDocument)
            : new MicrosoftTextBasedExtractionResult(MicrosoftTextBasedExtractionStatus.Success, units);
    }

    private static void AppendLeaves(
        XElement element,
        string path,
        string sourcePart,
        int maximumDepth,
        int currentDepth,
        int maximumCharacters,
        List<MicrosoftTextBasedExtractedUnit> units,
        ref int characterCount)
    {
        if (currentDepth > maximumDepth)
        {
            throw new ExtractionLimitExceededException();
        }

        var children = element.Elements().ToArray();
        if (children.Length == 0)
        {
            var text = Normalize(element.Value);
            if (text.Length == 0)
            {
                return;
            }

            var line = $"{path} : {text}";
            var newCharacterCount = checked(characterCount + line.Length);
            if (newCharacterCount > maximumCharacters)
            {
                throw new ExtractionLimitExceededException();
            }

            characterCount = newCharacterCount;
            units.Add(new MicrosoftTextBasedExtractedUnit(units.Count, line, sourcePart, IsTitle: false));
            return;
        }

        var siblingCounts = children
            .GroupBy(child => child.Name.LocalName)
            .ToDictionary(group => group.Key, group => group.Count());
        var seenCounts = new Dictionary<string, int>();
        foreach (var child in children)
        {
            var localName = child.Name.LocalName;
            var childPath = siblingCounts[localName] > 1
                ? $"{path}.{localName}[{seenCounts.GetValueOrDefault(localName)}]"
                : $"{path}.{localName}";
            seenCounts[localName] = seenCounts.GetValueOrDefault(localName) + 1;
            AppendLeaves(
                child,
                childPath,
                sourcePart,
                maximumDepth,
                currentDepth + 1,
                maximumCharacters,
                units,
                ref characterCount);
        }
    }

    private static XmlReader CreateSecureXmlReader(Stream stream) =>
        XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 100_000_000
        });

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed class ExtractionLimitExceededException : Exception;
}
