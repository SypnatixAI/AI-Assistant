using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365DriveSynchronizationService
{
    Task<Microsoft365DriveInitialSynchronizationResult> StartInitialSynchronizationAsync(
        Guid sourceId,
        Guid synchronizationId,
        CancellationToken cancellationToken = default);

    Task<Microsoft365DriveDeltaSynchronizationResult> StartDeltaSynchronizationAsync(
        Guid sourceId,
        Guid synchronizationId,
        CancellationToken cancellationToken = default);
}
