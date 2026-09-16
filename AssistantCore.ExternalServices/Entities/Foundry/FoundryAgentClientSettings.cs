namespace AssistantCore.ExternalServices.Entities.Foundry;

public sealed record FoundryAgentClientSettings(
    string ProjectEndpoint,
    string AgentName,
    string AgentVersion,
    bool RequireEnterpriseSearch = true,
    bool RequireWorkIq = false,
    string? WorkIqServerLabel = null,
    bool RequireFoundryIq = false,
    string? FoundryIqServerLabel = null);
