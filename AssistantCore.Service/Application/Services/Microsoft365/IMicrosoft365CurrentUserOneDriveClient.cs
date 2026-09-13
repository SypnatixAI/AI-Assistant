namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365CurrentUserOneDriveClient
{
    Task<Microsoft365CurrentUserDrive?> GetAsync(
        string tenantId,
        string entraUserId,
        CancellationToken cancellationToken = default);
}

public sealed record Microsoft365CurrentUserDrive(
    string DriveId,
    string DisplayName,
    string? WebUrl);
