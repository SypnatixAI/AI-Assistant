using System.Text;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftPlainTextContentExtractorClient
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md"
    };

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain", "text/markdown", "application/octet-stream"
    };

    public async Task<MicrosoftTextBasedExtractionResult> ExtractAsync(
        string fileName,
        string? mimeType,
        Stream content,
        long? contentLength,
        long maximumFileSizeBytes,
        int maximumExtractedCharacters,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var extension = Path.GetExtension(fileName);
        if (!SupportedExtensions.Contains(extension))
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

        if (ContainsBinaryData(buffer))
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.CorruptedDocument);
        }

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        var isMarkdown = extension.Equals(".md", StringComparison.OrdinalIgnoreCase);

        try
        {
            var units = isMarkdown
                ? ExtractMarkdownUnits(text, fileName, maximumExtractedCharacters)
                : ExtractPlainTextUnits(text, fileName, maximumExtractedCharacters);

            return units.Count == 0
                ? MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.EmptyDocument)
                : new MicrosoftTextBasedExtractionResult(MicrosoftTextBasedExtractionStatus.Success, units);
        }
        catch (ExtractionLimitExceededException)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
        }
    }

    private static bool ContainsBinaryData(MemoryStream buffer)
    {
        var bytes = buffer.GetBuffer();
        var length = (int)Math.Min(buffer.Length, 8000);
        for (var index = 0; index < length; index++)
        {
            if (bytes[index] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<MicrosoftTextBasedExtractedUnit> ExtractPlainTextUnits(
        string text,
        string sourcePart,
        int maximumCharacters)
    {
        var units = new List<MicrosoftTextBasedExtractedUnit>();
        var characterCount = 0;
        foreach (var paragraph in SplitParagraphs(text))
        {
            AddUnit(units, paragraph, sourcePart, isTitle: false, maximumCharacters, ref characterCount);
        }

        return units;
    }

    private static IReadOnlyList<MicrosoftTextBasedExtractedUnit> ExtractMarkdownUnits(
        string text,
        string sourcePart,
        int maximumCharacters)
    {
        var units = new List<MicrosoftTextBasedExtractedUnit>();
        var characterCount = 0;
        foreach (var paragraph in SplitParagraphs(text))
        {
            var trimmed = paragraph.TrimStart();
            var headingLevel = 0;
            while (headingLevel < trimmed.Length && headingLevel < 6 && trimmed[headingLevel] == '#')
            {
                headingLevel++;
            }

            var isHeading = headingLevel is > 0 and <= 6
                && trimmed.Length > headingLevel
                && trimmed[headingLevel] == ' ';
            var value = isHeading ? trimmed[(headingLevel + 1)..].Trim() : paragraph;
            AddUnit(units, value, sourcePart, isHeading, maximumCharacters, ref characterCount);
        }

        return units;
    }

    private static IEnumerable<string> SplitParagraphs(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(paragraph => Normalize(paragraph))
            .Where(paragraph => paragraph.Length > 0);

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

    private sealed class ExtractionLimitExceededException : Exception;
}
