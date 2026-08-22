using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Repositories;

public sealed class Microsoft365SubscriptionRepository(AssistantCoreDbContext dbContext)
    : IMicrosoft365SubscriptionRepository
{
    public async Task<IReadOnlyCollection<Microsoft365Subscription>> GetMaintenanceCandidatesAsync(
        DateTimeOffset renewBefore,
        CancellationToken cancellationToken = default) =>
        await SubscriptionQuery()
            .Where(subscription =>
                subscription.Status == Microsoft365SubscriptionStatus.Pending
                || subscription.Status == Microsoft365SubscriptionStatus.RevocationRequired
                || subscription.Status == Microsoft365SubscriptionStatus.RenewalRequired
                || (subscription.Status == Microsoft365SubscriptionStatus.Active
                    && subscription.ExpiresAt <= renewBefore))
            .OrderBy(subscription => subscription.ExpiresAt)
            .ThenBy(subscription => subscription.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Microsoft365Subscription?> FindActiveForNotificationAsync(
        string microsoftSubscriptionId,
        CancellationToken cancellationToken = default) =>
        SubscriptionQuery()
            .SingleOrDefaultAsync(subscription =>
                subscription.MicrosoftSubscriptionId == microsoftSubscriptionId
                && subscription.Status == Microsoft365SubscriptionStatus.Active,
                cancellationToken);

    public Microsoft365Synchronization GetOrCreateDeltaSynchronization(
        Microsoft365Subscription subscription,
        DateTimeOffset requestedAt)
    {
        var synchronization = subscription.Microsoft365Source.Synchronizations
            .FirstOrDefault(candidate =>
                candidate.Type == Microsoft365SynchronizationType.Delta
                && candidate.Status is Microsoft365SynchronizationStatus.Pending
                    or Microsoft365SynchronizationStatus.Running
                    or Microsoft365SynchronizationStatus.TemporaryFailure);
        if (synchronization is not null)
        {
            return synchronization;
        }

        synchronization = new Microsoft365Synchronization
        {
            Id = Guid.NewGuid(),
            Microsoft365SourceId = subscription.Microsoft365SourceId,
            Type = Microsoft365SynchronizationType.Delta,
            Status = Microsoft365SynchronizationStatus.Pending,
            AttemptCount = 0,
            RequestedAt = requestedAt
        };
        subscription.Microsoft365Source.Synchronizations.Add(synchronization);
        return synchronization;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<Microsoft365Subscription> SubscriptionQuery() =>
        dbContext.Microsoft365Subscriptions
            .Include(subscription => subscription.Microsoft365Source)
                .ThenInclude(source => source.Microsoft365Connection)
                    .ThenInclude(connection => connection.OrganizationConnector)
            .Include(subscription => subscription.Microsoft365Source)
                .ThenInclude(source => source.Synchronizations);
}
