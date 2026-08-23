using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Repositories;

public interface IMicrosoft365ListSynchronizationRepository
{
    Task<Microsoft365List?> FindForSynchronizationAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default);

    Task<bool> SaveSchemaAsync(
        Guid sourceId,
        Guid leaseId,
        string schemaFingerprint,
        bool requiresItemReprocessing,
        CancellationToken cancellationToken = default);

    Task<int> SaveWorkPageAsync(
        Guid sourceId,
        Guid synchronizationId,
        Guid leaseId,
        DateTimeOffset leaseExpiresAt,
        IReadOnlyCollection<Microsoft365ListItemWorkData> works,
        CancellationToken cancellationToken = default);

}
