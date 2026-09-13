namespace AssistantCore.ExternalServices.Entities.Foundry;

public sealed record FoundryAgentClientSettings(
    string ProjectEndpoint,
    string AgentName,
    string AgentVersion);
