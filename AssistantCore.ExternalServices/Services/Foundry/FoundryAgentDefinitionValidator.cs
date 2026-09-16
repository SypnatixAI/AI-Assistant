using System.Text.Json;
using AssistantCore.ExternalServices.Entities.Foundry;

namespace AssistantCore.ExternalServices.Services.Foundry;

public static class FoundryAgentDefinitionValidator
{
    private const string EnterpriseSearchToolName = "EnterpriseSearch";
    private const string WorkIqToolType = "work_iq_preview";
    private const string McpToolType = "mcp";
    private const string DefaultWorkIqServerLabel = "work-iq";
    private const string DefaultFoundryIqServerLabel = "foundry-iq";

    public static void Validate(JsonElement definition) =>
        Validate(
            definition,
            new FoundryAgentClientSettings(
                ProjectEndpoint: string.Empty,
                AgentName: string.Empty,
                AgentVersion: string.Empty,
                RequireEnterpriseSearch: true,
                RequireWorkIq: true,
                WorkIqServerLabel: DefaultWorkIqServerLabel,
                RequireFoundryIq: true,
                FoundryIqServerLabel: DefaultFoundryIqServerLabel));

    public static void Validate(
        JsonElement definition,
        FoundryAgentClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!definition.TryGetProperty("tools", out var tools)
            || tools.ValueKind != JsonValueKind.Array)
        {
            throw CreateInvalidConfigurationException(settings);
        }

        var configuredTools = tools.EnumerateArray().ToArray();
        var hasEnterpriseSearch = configuredTools.Any(IsEnterpriseSearch);
        var hasWorkIq = configuredTools.Any(tool => IsWorkIq(tool, settings.WorkIqServerLabel));
        var hasFoundryIq = configuredTools.Any(tool =>
            IsMcpServer(tool, settings.FoundryIqServerLabel));
        var hasWebSearch = configuredTools.Any(tool =>
            HasStringProperty(tool, "type", "web_search"));

        if (hasWebSearch
            || (settings.RequireEnterpriseSearch && !hasEnterpriseSearch)
            || (settings.RequireWorkIq && !hasWorkIq)
            || (settings.RequireFoundryIq && !hasFoundryIq))
        {
            throw CreateInvalidConfigurationException(settings);
        }
    }

    private static bool IsEnterpriseSearch(JsonElement tool) =>
        HasStringProperty(tool, "type", "function")
        && (HasStringProperty(tool, "name", EnterpriseSearchToolName)
            || (tool.TryGetProperty("function", out var function)
                && HasStringProperty(function, "name", EnterpriseSearchToolName)));

    private static bool IsWorkIq(JsonElement tool, string? serverLabel) =>
        HasStringProperty(tool, "type", WorkIqToolType)
        || IsMcpServer(tool, serverLabel);

    private static bool IsMcpServer(JsonElement tool, string? serverLabel)
    {
        if (string.IsNullOrWhiteSpace(serverLabel)
            || !HasStringProperty(tool, "type", McpToolType))
        {
            return false;
        }

        return HasStringProperty(tool, "server_label", serverLabel)
            || (tool.TryGetProperty("mcp", out var mcp)
                && HasStringProperty(mcp, "server_label", serverLabel));
    }

    private static bool HasStringProperty(
        JsonElement element,
        string propertyName,
        string expectedValue) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
        && string.Equals(property.GetString(), expectedValue, StringComparison.Ordinal);

    private static InvalidOperationException CreateInvalidConfigurationException(
        FoundryAgentClientSettings settings)
    {
        var requirements = new List<string>();
        if (settings.RequireEnterpriseSearch)
        {
            requirements.Add($"local function '{EnterpriseSearchToolName}'");
        }
        if (settings.RequireWorkIq)
        {
            requirements.Add(
                $"Work IQ ('{WorkIqToolType}' or MCP server '{settings.WorkIqServerLabel}')");
        }
        if (settings.RequireFoundryIq)
        {
            requirements.Add($"Foundry IQ MCP server '{settings.FoundryIqServerLabel}'");
        }

        var expected = requirements.Count == 0
            ? "the configured managed knowledge tools"
            : string.Join(", ", requirements);

        return new InvalidOperationException(
            $"The configured Foundry prompt agent must declare {expected} and must not declare 'web_search'.");
    }
}
