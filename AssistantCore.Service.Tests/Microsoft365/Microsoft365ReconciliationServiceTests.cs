using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ReconciliationServiceTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-08-22T12:00:00Z");

    [Theory, AutoDomainData]
    public async Task Given_DueActiveSources_When_RunReconciliationAsync_Then_DeltaWorkIsPublishedAndNextRunIsScheduled(
        Guid organizationId,
        string subscriptionId,
        string siteId,
        string listId,
        string driveId)
    {
        // Given
        var listSubscription = CreateListSubscription(
            organizationId,
            subscriptionId,
            siteId,
            listId);
        var existingSynchronization = new Microsoft365Synchronization
        {
            Id = Guid.NewGuid(),
            Microsoft365SourceId = listSubscription.Microsoft365SourceId,
            Type = Microsoft365SynchronizationType.Delta,
            Status = Microsoft365SynchronizationStatus.Pending,
            RequestedAt = Now.AddMinutes(-1)
        };
        listSubscription.Microsoft365Source.Synchronizations.Add(existingSynchronization);
        var driveSubscription = CreateDriveSubscription(
            organizationId,
            $"{subscriptionId}-drive",
            siteId,
            driveId);
        var repository = new SubscriptionRepositoryFake(
            [listSubscription, driveSubscription]);
        var publisher = new SynchronizationPublisherFake();
        var service = new Microsoft365ReconciliationService(
            repository,
            publisher,
            Options.Create(new Microsoft365Options
            {
                SynchronizationIntervalMinutes = 15
            }),
            new FixedTimeProvider(Now),
            NullLogger<Microsoft365ReconciliationService>.Instance);

        // When
        await service.RunReconciliationAsync(CancellationToken.None);

        // Then
        Assert.Equal(Now, repository.RequestedDueAt);
        Assert.Equal(2, repository.SaveCount);
        Assert.Equal(Now.AddMinutes(15), listSubscription.Microsoft365Source.NextSynchronizationAt);
        Assert.Equal(Now.AddMinutes(15), driveSubscription.Microsoft365Source.NextSynchronizationAt);
        Assert.Contains(publisher.Published, work =>
            work.WorkId == existingSynchronization.Id
            && work.WorkType == "SynchronizeList"
            && work.ListId == listId);
        Assert.Contains(publisher.Published, work =>
            work.WorkType == "SynchronizeDrive"
            && work.DriveId == driveId);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoDueSource_When_RunReconciliationAsync_Then_NoDeltaWorkIsPublished(
        bool _)
    {
        // Given
        var repository = new SubscriptionRepositoryFake([]);
        var publisher = new SynchronizationPublisherFake();
        var service = new Microsoft365ReconciliationService(
            repository,
            publisher,
            Options.Create(new Microsoft365Options()),
            new FixedTimeProvider(Now),
            NullLogger<Microsoft365ReconciliationService>.Instance);

        // When
        await service.RunReconciliationAsync(CancellationToken.None);

        // Then
        Assert.Equal(0, repository.SaveCount);
        Assert.Empty(publisher.Published);
    }

    private static Microsoft365Subscription CreateListSubscription(
        Guid organizationId,
        string subscriptionId,
        string siteId,
        string listId) =>
        CreateSubscription(
            organizationId,
            subscriptionId,
            new Microsoft365List
            {
                Id = Guid.NewGuid(),
                SiteId = siteId,
                ListId = listId,
                Kind = Microsoft365SourceKind.SharePointList
            });

    private static Microsoft365Subscription CreateDriveSubscription(
        Guid organizationId,
        string subscriptionId,
        string siteId,
        string driveId) =>
        CreateSubscription(
            organizationId,
            subscriptionId,
            new Microsoft365Drive
            {
                Id = Guid.NewGuid(),
                SiteId = siteId,
                DriveId = driveId,
                Kind = Microsoft365SourceKind.SharePointDrive
            });

    private static Microsoft365Subscription CreateSubscription(
        Guid organizationId,
        string subscriptionId,
        Microsoft365Source source)
    {
        source.Status = Microsoft365SourceStatus.Enabled;
        source.IsIndexed = true;
        source.DeltaLink = "opaque-delta-link";
        var subscription = new Microsoft365Subscription
        {
            Id = Guid.NewGuid(),
            Microsoft365SourceId = source.Id,
            OrganizationId = organizationId,
            MicrosoftSubscriptionId = subscriptionId,
            Status = Microsoft365SubscriptionStatus.Active,
            Microsoft365Source = source
        };
        source.Subscriptions.Add(subscription);
        return subscription;
    }

    private sealed class SubscriptionRepositoryFake(
        IReadOnlyCollection<Microsoft365Subscription> candidates)
        : IMicrosoft365SubscriptionRepository
    {
        public DateTimeOffset? RequestedDueAt { get; private set; }

        public int SaveCount { get; private set; }

        public Task<IReadOnlyCollection<Microsoft365Subscription>> GetMaintenanceCandidatesAsync(
            DateTimeOffset renewBefore,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365Subscription>>([]);

        public Task<IReadOnlyCollection<Microsoft365Subscription>> GetReconciliationCandidatesAsync(
            DateTimeOffset dueAt,
            CancellationToken cancellationToken = default)
        {
            RequestedDueAt = dueAt;
            return Task.FromResult(candidates);
        }

        public Task<Microsoft365Subscription?> FindActiveForNotificationAsync(
            string microsoftSubscriptionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Microsoft365Subscription?>(null);

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
                RequestedAt = requestedAt
            };
            subscription.Microsoft365Source.Synchronizations.Add(synchronization);
            return synchronization;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class SynchronizationPublisherFake : IMicrosoft365SynchronizationPublisher
    {
        public List<Microsoft365SynchronizationWork> Published { get; } = [];

        public Task PublishAsync(
            Microsoft365SynchronizationWork work,
            CancellationToken cancellationToken = default)
        {
            Published.Add(work);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
