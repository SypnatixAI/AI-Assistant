namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftDrive(
    string Id,
    string Name,
    string? WebUrl,
    string? DriveType);
