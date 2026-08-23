using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ListSynchronizationService
{
    Task<Microsoft365ListInitialSynchronizationResult> StartInitialSynchronizationAsync(
        Guid sourceId,
        Guid synchronizationId,
        CancellationToken cancellationToken = default);

    Task<Microsoft365ListDeltaSynchronizationResult> StartDeltaSynchronizationAsync(
        Guid sourceId,
        Guid synchronizationId,
        CancellationToken cancellationToken = default);

    Task<Microsoft365ListSchemaSynchronizationResult> SynchronizeSchemaAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default);
}
