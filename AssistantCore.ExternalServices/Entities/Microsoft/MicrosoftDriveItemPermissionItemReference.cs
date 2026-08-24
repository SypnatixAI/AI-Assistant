namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftDriveItemPermissionItemReference(
    string? DriveId,
    string? Id,
    string? Path);
