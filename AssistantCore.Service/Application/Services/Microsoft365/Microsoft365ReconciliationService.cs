using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365ReconciliationService(
    IMicrosoft365SubscriptionRepository subscriptionRepository,
    IMicrosoft365SynchronizationPublisher synchronizationPublisher,
    IOptions<Microsoft365Options> options,
    TimeProvider timeProvider,
    ILogger<Microsoft365ReconciliationService> logger) : IMicrosoft365ReconciliationService
{
    public async Task RunReconciliationAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var candidates = await subscriptionRepository.GetReconciliationCandidatesAsync(
            now,
            cancellationToken);

        foreach (var subscription in candidates)
        {
            try
            {
                var synchronization = subscriptionRepository.GetOrCreateDeltaSynchronization(
                    subscription,
                    now);
                subscription.Microsoft365Source.NextSynchronizationAt = now.AddMinutes(
                    options.Value.SynchronizationIntervalMinutes);

                await subscriptionRepository.SaveChangesAsync(cancellationToken);

                var work = Microsoft365SynchronizationWorkFactory.Create(
                    subscription,
                    synchronization)
                    ?? throw new InvalidOperationException(
                        "Microsoft 365 source type cannot be synchronized.");
                await synchronizationPublisher.PublishAsync(work, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Microsoft 365 reconciliation failed. SubscriptionRecordId: {SubscriptionRecordId}; SourceId: {SourceId}.",
                    subscription.Id,
                    subscription.Microsoft365SourceId);
            }
        }
    }
}
