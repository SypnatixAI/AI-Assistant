using System.Text.Json;
using AssistantCore.Service.Application.Exceptions;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public sealed class AiToolArgumentSecurityValidator : IAiToolArgumentSecurityValidator
{
    private static readonly HashSet<string> ForbiddenTechnicalFieldNames = new(
        [
            "url",
            "providerUrl",
            "endpoint",
            "sql",
            "organizationId",
            "tenantId",
            "token",
            "accessToken",
            "apiKey",
            "key",
            "index",
            "indexName",
            "filter",
            "odataFilter"
        ],
        StringComparer.OrdinalIgnoreCase);

    public void Validate(JsonElement arguments, string toolCallId)
    {
        ValidateTechnicalFields(arguments, toolCallId);
    }

    private static void ValidateTechnicalFields(JsonElement value, string toolCallId)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (ForbiddenTechnicalFieldNames.Contains(property.Name))
                {
                    throw new ToolCallValidationException(
                        toolCallId,
                        $"Technical field '{property.Name}' is not allowed.");
                }

                ValidateTechnicalFields(property.Value, toolCallId);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                ValidateTechnicalFields(item, toolCallId);
            }
        }
    }
}
