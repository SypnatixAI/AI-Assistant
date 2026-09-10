using System.Text.Json;

namespace AssistantCore.ExternalServices.Services.Foundry;

public static class FoundryAgentDefinitionValidator
{
    private const string EnterpriseSearchToolName = "EnterpriseSearch";

    public static void Validate(JsonElement definition)
    {
        if (!definition.TryGetProperty("tools", out var tools)
            || tools.ValueKind != JsonValueKind.Array)
        {
            throw CreateInvalidConfigurationException();
        }

        var hasEnterpriseSearch = tools.EnumerateArray().Any(IsEnterpriseSearch);
        var hasWebSearch = tools.EnumerateArray().Any(tool =>
            HasStringProperty(tool, "type", "web_search"));

        if (!hasEnterpriseSearch || hasWebSearch)
        {
            throw CreateInvalidConfigurationException();
        }
    }

    private static bool IsEnterpriseSearch(JsonElement tool) =>
        HasStringProperty(tool, "type", "function")
        && (HasStringProperty(tool, "name", EnterpriseSearchToolName)
            || (tool.TryGetProperty("function", out var function)
                && HasStringProperty(function, "name", EnterpriseSearchToolName)));

    private static bool HasStringProperty(
        JsonElement element,
        string propertyName,
        string expectedValue) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
        && string.Equals(property.GetString(), expectedValue, StringComparison.Ordinal);

    private static InvalidOperationException CreateInvalidConfigurationException() =>
        new(
            "The configured Foundry prompt agent must declare the local function tool "
            + $"'{EnterpriseSearchToolName}' and must not declare 'web_search'.");
}
