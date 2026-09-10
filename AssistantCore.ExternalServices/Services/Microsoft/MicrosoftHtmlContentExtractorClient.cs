using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed partial class MicrosoftHtmlContentExtractorClient
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm"
    };

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/html", "application/xhtml+xml", "text/plain", "application/octet-stream"
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

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var html = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(html))
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.EmptyDocument);
        }

        var units = new List<MicrosoftTextBasedExtractedUnit>();
        var characterCount = 0;

        try
        {
            foreach (var title in ExtractTitleTags(html))
            {
                AddUnit(units, title, fileName, isTitle: true, maximumExtractedCharacters, ref characterCount);
            }

            var withoutActiveContent = ScriptTagPattern().Replace(html, " ");
            withoutActiveContent = StyleTagPattern().Replace(withoutActiveContent, " ");
            // Les titres sont deja captures separement ci-dessus : on retire tout le bloc
            // (balises et texte) pour ne jamais dupliquer leur contenu comme paragraphe.
            withoutActiveContent = HeadingTagPattern().Replace(withoutActiveContent, "\n");
            var withLineBreaks = BlockBoundaryPattern().Replace(withoutActiveContent, "\n");
            var withoutTags = TagPattern().Replace(withLineBreaks, string.Empty);
            var decoded = WebUtility.HtmlDecode(withoutTags);

            foreach (var paragraph in SplitParagraphs(decoded))
            {
                AddUnit(units, paragraph, fileName, isTitle: false, maximumExtractedCharacters, ref characterCount);
            }
        }
        catch (ExtractionLimitExceededException)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
        }

        return units.Count == 0
            ? MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.EmptyDocument)
            : new MicrosoftTextBasedExtractionResult(MicrosoftTextBasedExtractionStatus.Success, units);
    }

    private static IEnumerable<string> ExtractTitleTags(string html)
    {
        foreach (Match match in HeadingTagPattern().Matches(html))
        {
            var text = Normalize(WebUtility.HtmlDecode(TagPattern().Replace(match.Groups["text"].Value, string.Empty)));
            if (text.Length > 0)
            {
                yield return text;
            }
        }
    }

    private static IEnumerable<string> SplitParagraphs(string text) =>
        text.Split('\n')
            .Select(Normalize)
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

    [GeneratedRegex(@"<script[^>]*>.*?</script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptTagPattern();

    [GeneratedRegex(@"<style[^>]*>.*?</style\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleTagPattern();

    [GeneratedRegex(@"<h[1-6][^>]*>(?<text>.*?)</h[1-6]\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HeadingTagPattern();

    [GeneratedRegex(@"</(p|div|li|h[1-6]|tr|br)\s*>|<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBoundaryPattern();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex TagPattern();

    private sealed class ExtractionLimitExceededException : Exception;
}
