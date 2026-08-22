namespace AssistantCore.Repository.Repositories;

public sealed record Microsoft365SourceDiscoveryData(
    string MicrosoftResourceId,
    string DisplayName,
    string? WebUrl);
