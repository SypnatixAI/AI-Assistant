using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class MicrosoftGraphNotificationService(
    IMicrosoft365SubscriptionRepository subscriptionRepository,
    IMicrosoft365ClientStateProtector clientStateProtector,
    IMicrosoft365SynchronizationPublisher synchronizationPublisher,
    TimeProvider timeProvider) : IMicrosoftGraphNotificationService
{
    public async Task HandleNotificationsAsync(
        IReadOnlyCollection<MicrosoftGraphNotification> notifications,
        CancellationToken cancellationToken = default)
    {
        var publishedWorkIds = new HashSet<Guid>();
        foreach (var notification in notifications)
        {
            if (string.IsNullOrWhiteSpace(notification.SubscriptionId)
                || string.IsNullOrWhiteSpace(notification.ClientState)
                || string.IsNullOrWhiteSpace(notification.TenantId))
            {
                continue;
            }

            var subscription = await subscriptionRepository.FindActiveForNotificationAsync(
                notification.SubscriptionId,
                cancellationToken);
            var now = timeProvider.GetUtcNow();
            if (subscription is null
                || subscription.ExpiresAt is null
                || subscription.ExpiresAt <= now
                || string.IsNullOrWhiteSpace(subscription.ProtectedClientState)
                || !clientStateProtector.Matches(
                    notification.ClientState,
                    subscription.ProtectedClientState))
            {
                continue;
            }

            var source = subscription.Microsoft365Source;
            var connection = source.Microsoft365Connection;
            if (!source.IsIndexed
                || source.Status != Microsoft365SourceStatus.Enabled
                || connection.Status != Microsoft365ConnectionStatus.Active
                || connection.OrganizationConnector.Status != RecordStatus.Active
                || !string.Equals(
                    notification.TenantId,
                    connection.TenantId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var synchronization = subscriptionRepository.GetOrCreateDeltaSynchronization(
                subscription,
                now);
            await subscriptionRepository.SaveChangesAsync(cancellationToken);
            var work = Microsoft365SynchronizationWorkFactory.Create(subscription, synchronization);
            if (work is not null && publishedWorkIds.Add(work.WorkId))
            {
                await synchronizationPublisher.PublishAsync(work, cancellationToken);
            }
        }
    }
}
