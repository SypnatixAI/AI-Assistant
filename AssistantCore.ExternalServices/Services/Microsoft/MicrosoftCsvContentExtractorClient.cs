using System.Text;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftCsvContentExtractorClient
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv"
    };

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/csv", "application/csv", "text/plain", "application/octet-stream"
    };

    public async Task<MicrosoftTextBasedExtractionResult> ExtractAsync(
        string fileName,
        string? mimeType,
        Stream content,
        long? contentLength,
        long maximumFileSizeBytes,
        int maximumRows,
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

        IReadOnlyList<IReadOnlyList<string>> rows;
        try
        {
            rows = ParseRows(text);
        }
        catch (FormatException)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.CorruptedDocument);
        }

        if (rows.Count == 0)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.EmptyDocument);
        }

        if (rows.Count > maximumRows)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
        }

        var headers = rows[0];
        var dataRows = rows.Skip(1).ToArray();
        if (dataRows.Length == 0)
        {
            return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.EmptyDocument);
        }

        var units = new List<MicrosoftTextBasedExtractedUnit>();
        var characterCount = 0;
        for (var rowIndex = 0; rowIndex < dataRows.Length; rowIndex++)
        {
            var row = dataRows[rowIndex];
            var fields = row.Select((value, columnIndex) =>
            {
                var header = columnIndex < headers.Count ? headers[columnIndex] : string.Empty;
                return string.IsNullOrWhiteSpace(header)
                    ? $"Colonne {columnIndex + 1} : {value}"
                    : $"{header} : {value}";
            });
            var line = $"Ligne {rowIndex + 1} — {string.Join(" | ", fields)}";

            var newCharacterCount = checked(characterCount + line.Length);
            if (newCharacterCount > maximumExtractedCharacters)
            {
                return MicrosoftTextBasedExtractionResult.Empty(MicrosoftTextBasedExtractionStatus.TooLarge);
            }

            characterCount = newCharacterCount;
            units.Add(new MicrosoftTextBasedExtractedUnit(units.Count, line, fileName, IsTitle: false));
        }

        return new MicrosoftTextBasedExtractionResult(MicrosoftTextBasedExtractionStatus.Success, units);
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseRows(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var index = 0;
        var rowHasContent = false;

        while (index < text.Length)
        {
            var current = text[index];

            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index += 2;
                        continue;
                    }

                    inQuotes = false;
                    index++;
                    continue;
                }

                field.Append(current);
                index++;
                continue;
            }

            switch (current)
            {
                case '"':
                    inQuotes = true;
                    rowHasContent = true;
                    index++;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    rowHasContent = true;
                    index++;
                    break;
                case '\r':
                    index++;
                    break;
                case '\n':
                    if (rowHasContent || field.Length > 0 || fields.Count > 0)
                    {
                        fields.Add(field.ToString());
                        rows.Add(fields.ToArray());
                        fields.Clear();
                        field.Clear();
                        rowHasContent = false;
                    }

                    index++;
                    break;
                default:
                    field.Append(current);
                    rowHasContent = true;
                    index++;
                    break;
            }
        }

        if (inQuotes)
        {
            throw new FormatException("The CSV content has an unterminated quoted field.");
        }

        if (rowHasContent || field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            rows.Add(fields.ToArray());
        }

        return rows;
    }
}
