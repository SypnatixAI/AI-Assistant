namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365CurrentUserOneDriveIndexingService
{
    Task EnsureIndexedAsync(
        Guid organizationId,
        string entraUserId,
        CancellationToken cancellationToken = default);
}
