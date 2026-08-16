using System.Globalization;
using System.Text.Json;
using AssistantCore.Service.Application.Exceptions;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public sealed class AiToolDateRangeValidator : IAiToolDateRangeValidator
{
    public void Validate(JsonElement arguments, string toolCallId)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var dateFrom = ReadDate(arguments, "dateFrom", toolCallId);
        var dateTo = ReadDate(arguments, "dateTo", toolCallId);

        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
        {
            throw new ToolCallValidationException(
                toolCallId,
                "Field 'dateFrom' cannot be later than 'dateTo'.");
        }
    }

    private static DateOnly? ReadDate(
        JsonElement arguments,
        string propertyName,
        string toolCallId)
    {
        if (!arguments.TryGetProperty(propertyName, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String
            || !DateOnly.TryParseExact(
                value.GetString(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            throw new ToolCallValidationException(
                toolCallId,
                $"Field '{propertyName}' must use the YYYY-MM-DD format.");
        }

        return date;
    }
}
