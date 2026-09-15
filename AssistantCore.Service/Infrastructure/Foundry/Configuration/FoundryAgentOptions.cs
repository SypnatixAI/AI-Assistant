namespace AssistantCore.Service.Infrastructure.Foundry.Configuration;

public sealed class FoundryAgentOptions
{
    public const string SectionName = "FoundryAgent";

    public string ProjectEndpoint { get; init; } = string.Empty;

    public string AgentName { get; init; } = string.Empty;

    public string AgentVersion { get; init; } = string.Empty;

    public bool RequireEnterpriseSearch { get; init; } = true;

    public bool RequireWorkIq { get; init; }

    public string? WorkIqServerLabel { get; init; }

    public bool RequireFoundryIq { get; init; }

    public string? FoundryIqServerLabel { get; init; }

    public bool IsValid() =>
        Uri.TryCreate(ProjectEndpoint, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(AgentName)
        && !string.IsNullOrWhiteSpace(AgentVersion)
        && (!RequireWorkIq || !string.IsNullOrWhiteSpace(WorkIqServerLabel))
        && (!RequireFoundryIq || !string.IsNullOrWhiteSpace(FoundryIqServerLabel));
}
