namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftDriveItemPermissionIdentity(
    string? Id,
    string? DisplayName,
    string? LoginName);
