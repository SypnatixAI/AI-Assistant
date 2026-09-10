using System.Text;
using System.Text.Json;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftJsonContentExtractorClient
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json"
    };

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json", "text/plain", "application/octet-stream"
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

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.EmptyDocument);
        }

        if (ExceedsMaximumDepth(text, maximumDepth))
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
        }

        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = maximumDepth });
        }
        catch (JsonException)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.CorruptedDocument);
        }

        using (document)
        {
            var units = new List<MicrosoftTextBasedExtractedUnit>();
            var characterCount = 0;
            try
            {
                AppendLeaves(
                    document.RootElement,
                    "$",
                    fileName,
                    maximumExtractedCharacters,
                    units,
                    ref characterCount);
            }
            catch (ExtractionLimitExceededException)
            {
                return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
            }

            return units.Count == 0
                ? MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.EmptyDocument)
                : new MicrosoftTextBasedExtractionResult(MicrosoftTextBasedExtractionStatus.Success, units);
        }
    }

    private static bool ExceedsMaximumDepth(string text, int maximumDepth)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        foreach (var character in text)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inString = true;
                    break;
                case '{' or '[':
                    depth++;
                    if (depth > maximumDepth)
                    {
                        return true;
                    }

                    break;
                case '}' or ']':
                    depth--;
                    break;
            }
        }

        return false;
    }

    private static void AppendLeaves(
        JsonElement element,
        string path,
        string sourcePart,
        int maximumCharacters,
        List<MicrosoftTextBasedExtractedUnit> units,
        ref int characterCount)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    AppendLeaves(
                        property.Value,
                        $"{path}.{property.Name}",
                        sourcePart,
                        maximumCharacters,
                        units,
                        ref characterCount);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    AppendLeaves(item, $"{path}[{index}]", sourcePart, maximumCharacters, units, ref characterCount);
                    index++;
                }

                break;
            case JsonValueKind.Undefined:
                break;
            default:
                var line = $"{path.TrimStart('$', '.')} : {FormatScalar(element)}";
                var newCharacterCount = checked(characterCount + line.Length);
                if (newCharacterCount > maximumCharacters)
                {
                    throw new ExtractionLimitExceededException();
                }

                characterCount = newCharacterCount;
                units.Add(new MicrosoftTextBasedExtractedUnit(units.Count, line, sourcePart, IsTitle: false));
                break;
        }
    }

    private static string FormatScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => element.GetRawText()
    };

    private sealed class ExtractionLimitExceededException : Exception;
}
