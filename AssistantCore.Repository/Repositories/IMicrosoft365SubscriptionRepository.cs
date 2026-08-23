using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Repositories;

public interface IMicrosoft365SubscriptionRepository
{
    Task<IReadOnlyCollection<Microsoft365Subscription>> GetMaintenanceCandidatesAsync(
        DateTimeOffset renewBefore,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Microsoft365Subscription>> GetReconciliationCandidatesAsync(
        DateTimeOffset dueAt,
        CancellationToken cancellationToken = default);

    Task<Microsoft365Subscription?> FindActiveForNotificationAsync(
        string microsoftSubscriptionId,
        CancellationToken cancellationToken = default);

    Microsoft365Synchronization GetOrCreateDeltaSynchronization(
        Microsoft365Subscription subscription,
        DateTimeOffset requestedAt);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
