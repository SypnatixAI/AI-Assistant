using System.Text;

namespace AssistantCore.ExternalServices.Services.Microsoft;

internal static class MicrosoftCsvRowParser
{
    public static IReadOnlyList<string>? TryParse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var fields = new List<string>();
        var currentField = new StringBuilder();
        var insideQuotedField = false;
        var quotedFieldClosed = false;

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (insideQuotedField)
            {
                if (character != '"')
                {
                    currentField.Append(character);
                    continue;
                }

                if (index + 1 < value.Length && value[index + 1] == '"')
                {
                    currentField.Append('"');
                    index++;
                    continue;
                }

                insideQuotedField = false;
                quotedFieldClosed = true;
                continue;
            }

            if (character == ',')
            {
                fields.Add(currentField.ToString());
                currentField.Clear();
                quotedFieldClosed = false;
                continue;
            }

            if (character == '"' && currentField.Length == 0 && !quotedFieldClosed)
            {
                insideQuotedField = true;
                continue;
            }

            if (quotedFieldClosed)
            {
                return null;
            }

            currentField.Append(character);
        }

        if (insideQuotedField)
        {
            return null;
        }

        fields.Add(currentField.ToString());
        return fields;
    }
}
