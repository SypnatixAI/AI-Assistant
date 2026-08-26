using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365PendingSynchronizationService(
    IMicrosoft365PendingSynchronizationRepository repository,
    IMicrosoft365DriveSynchronizationService driveSynchronizationService,
    TimeProvider timeProvider) : IMicrosoft365PendingSynchronizationService
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var work = await repository.ClaimNextDriveAsync(
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (work is null)
        {
            return false;
        }

        if (work.Type == Microsoft365SynchronizationType.Initial)
        {
            await driveSynchronizationService.StartInitialSynchronizationAsync(
                work.SourceId,
                work.SynchronizationId,
                cancellationToken);
        }
        else
        {
            await driveSynchronizationService.StartDeltaSynchronizationAsync(
                work.SourceId,
                work.SynchronizationId,
                cancellationToken);
        }

        return true;
    }
}
