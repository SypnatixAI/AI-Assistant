namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftList(
    string Id,
    string DisplayName,
    string? WebUrl,
    bool IsHidden,
    string? Template,
    bool IsSystem,
    bool IsDeleted);
