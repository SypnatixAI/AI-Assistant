using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Repositories;

public interface IMicrosoft365SourceSynchronizationRepository
{
    Task<bool> TryAcquireLeaseAsync(
        Guid sourceId,
        Guid leaseId,
        DateTimeOffset attemptedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task<bool> ConfirmCheckpointAsync(
        Guid sourceId,
        Guid leaseId,
        string deltaLink,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default);

    Task<bool> MarkFullResyncRequiredAsync(
        Guid sourceId,
        Guid leaseId,
        string lastErrorCode,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAccessErrorAsync(
        Guid sourceId,
        Guid leaseId,
        string lastErrorCode,
        CancellationToken cancellationToken = default);

    Task<bool> RecordSynchronizationOutcomeAsync(
        Guid sourceId,
        Guid synchronizationId,
        Microsoft365SynchronizationStatus status,
        Microsoft365SynchronizationCounters counters,
        DateTimeOffset completedAt,
        string? lastErrorCode,
        CancellationToken cancellationToken = default);

    Task ReleaseLeaseAsync(
        Guid sourceId,
        Guid leaseId,
        string? lastErrorCode,
        CancellationToken cancellationToken = default);
}
