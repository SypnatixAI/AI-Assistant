using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Repositories;

public interface IMicrosoft365DriveSynchronizationRepository
{
    Task<Microsoft365Drive?> FindForSynchronizationAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default);

    Task<int> SaveWorkPageAsync(
        Guid sourceId,
        Guid synchronizationId,
        Guid leaseId,
        DateTimeOffset leaseExpiresAt,
        IReadOnlyCollection<Microsoft365DocumentWorkData> works,
        CancellationToken cancellationToken = default);
}
