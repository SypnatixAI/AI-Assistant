using AssistantCore.Repository.Repositories;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365CurrentUserOneDriveIndexingService(
    IMicrosoft365ConnectionRepository connectionRepository,
    IMicrosoft365DriveRepository driveRepository,
    IMicrosoft365SourceDiscoveryRepository sourceRepository,
    IMicrosoft365CurrentUserOneDriveClient oneDriveClient,
    TimeProvider timeProvider) : IMicrosoft365CurrentUserOneDriveIndexingService
{
    public async Task EnsureIndexedAsync(
        Guid organizationId,
        string entraUserId,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || string.IsNullOrWhiteSpace(entraUserId))
        {
            return;
        }

        var connection = await connectionRepository.FindActiveByOrganizationAsync(
            organizationId,
            cancellationToken);
        if (connection is null || string.IsNullOrWhiteSpace(connection.TenantId))
        {
            return;
        }

        var externalDrive = await oneDriveClient.GetAsync(
            connection.TenantId,
            entraUserId,
            cancellationToken);
        if (externalDrive is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var savedDrive = await driveRepository.SaveOneDriveAsync(
            connection,
            externalDrive.DriveId,
            entraUserId,
            ownerUserPrincipalName: null,
            externalDrive.DisplayName,
            externalDrive.WebUrl,
            now,
            cancellationToken);

        if (savedDrive.IsIndexed)
        {
            return;
        }

        var drive = await driveRepository.FindAsync(
            organizationId,
            externalDrive.DriveId,
            cancellationToken) ?? savedDrive;
        await sourceRepository.SaveDriveActivationAsync(
            drive,
            now,
            cancellationToken);
    }
}
