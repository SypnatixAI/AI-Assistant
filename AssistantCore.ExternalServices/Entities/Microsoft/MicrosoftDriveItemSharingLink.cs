namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftDriveItemSharingLink(
    string? Type,
    string? Scope,
    string? WebUrl,
    bool? PreventsDownload,
    MicrosoftDriveItemPermissionIdentity? Application);
