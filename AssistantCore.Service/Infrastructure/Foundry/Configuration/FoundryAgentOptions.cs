namespace AssistantCore.Service.Infrastructure.Foundry.Configuration;

public sealed class FoundryAgentOptions
{
    public const string SectionName = "FoundryAgent";

    public string ProjectEndpoint { get; init; } = string.Empty;

    public string AgentName { get; init; } = string.Empty;

    public string AgentVersion { get; init; } = string.Empty;

    public bool IsValid() =>
        Uri.TryCreate(ProjectEndpoint, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(AgentName)
        && !string.IsNullOrWhiteSpace(AgentVersion);
}
