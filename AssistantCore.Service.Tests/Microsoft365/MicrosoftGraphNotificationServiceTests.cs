using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftGraphNotificationServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_ListNotification_When_HandleNotificationsAsync_Then_AclAndContentReconciliationAreScheduled(
        Guid organizationId,
        string subscriptionId,
        string tenantId,
        string siteId,
        string listId)
    {
        // Given
        var subscription = CreateListSubscription(
            organizationId,
            subscriptionId,
            tenantId,
            siteId,
            listId);
        var repository = new SubscriptionRepositoryFake(subscription);
        var indexedContentRepository = new IndexedContentRepositoryFake();
        var service = CreateService(repository, indexedContentRepository);

        // When
        await service.HandleNotificationsAsync(
            [CreateNotification(subscriptionId, tenantId, "valid-client-state")],
            CancellationToken.None);

        // Then
        Assert.Equal(organizationId, subscription.OrganizationId);
        Assert.Equal(subscription.Microsoft365SourceId, indexedContentRepository.RequestedSourceId);
        Assert.Equal(
            DateTimeOffset.Parse("2026-08-21T12:00:00Z"),
            subscription.Microsoft365Source.NextSynchronizationAt);
        Assert.Single(subscription.Microsoft365Source.Synchronizations);
    }

    [Theory, AutoDomainData]
    public async Task Given_DriveNotification_When_HandleNotificationsAsync_Then_AclReconciliationPrecedesContentWake(
        Guid organizationId,
        string subscriptionId,
        string tenantId,
        string siteId,
        string driveId)
    {
        // Given
        var subscription = CreateDriveSubscription(
            organizationId,
            subscriptionId,
            tenantId,
            siteId,
            driveId);
        var indexedContentRepository = new IndexedContentRepositoryFake();
        var service = CreateService(
            new SubscriptionRepositoryFake(subscription),
            indexedContentRepository);

        // When
        await service.HandleNotificationsAsync(
            [CreateNotification(subscriptionId, tenantId, "valid-client-state")],
            CancellationToken.None);

        // Then
        Assert.Equal(subscription.Microsoft365SourceId, indexedContentRepository.RequestedSourceId);
        Assert.NotNull(subscription.Microsoft365Source.NextSynchronizationAt);
        Assert.Single(subscription.Microsoft365Source.Synchronizations);
    }

    [Theory, AutoDomainData]
    public async Task Given_InvalidClientState_When_HandleNotificationsAsync_Then_NoReconciliationIsScheduled(
        Guid organizationId,
        string subscriptionId,
        string tenantId,
        string siteId,
        string listId)
    {
        // Given
        var subscription = CreateListSubscription(
            organizationId,
            subscriptionId,
            tenantId,
            siteId,
            listId);
        var indexedContentRepository = new IndexedContentRepositoryFake();
        var service = CreateService(
            new SubscriptionRepositoryFake(subscription),
            indexedContentRepository);

        // When
        await service.HandleNotificationsAsync(
            [CreateNotification(subscriptionId, tenantId, "forged-client-state")],
            CancellationToken.None);

        // Then
        Assert.Null(indexedContentRepository.RequestedSourceId);
        Assert.Empty(subscription.Microsoft365Source.Synchronizations);
    }

    [Theory, AutoDomainData]
    public async Task Given_DisabledSource_When_HandleNotificationsAsync_Then_NoReconciliationIsScheduled(
        Guid organizationId,
        string subscriptionId,
        string tenantId,
        string siteId,
        string listId)
    {
        // Given
        var subscription = CreateListSubscription(
            organizationId,
            subscriptionId,
            tenantId,
            siteId,
            listId);
        subscription.Microsoft365Source.Status = Microsoft365SourceStatus.Disabled;
        subscription.Microsoft365Source.IsIndexed = false;
        var indexedContentRepository = new IndexedContentRepositoryFake();
        var service = CreateService(
            new SubscriptionRepositoryFake(subscription),
            indexedContentRepository);

        // When
        await service.HandleNotificationsAsync(
            [CreateNotification(subscriptionId, tenantId, "valid-client-state")],
            CancellationToken.None);

        // Then
        Assert.Null(indexedContentRepository.RequestedSourceId);
        Assert.Empty(subscription.Microsoft365Source.Synchronizations);
    }

    [Theory, AutoDomainData]
    public async Task Given_TwoIdenticalNotifications_When_HandleNotificationsAsync_Then_OneContentWakeIsScheduled(
        Guid organizationId,
        string subscriptionId,
        string tenantId,
        string siteId,
        string listId)
    {
        // Given
        var subscription = CreateListSubscription(
            organizationId,
            subscriptionId,
            tenantId,
            siteId,
            listId);
        var service = CreateService(
            new SubscriptionRepositoryFake(subscription),
            new IndexedContentRepositoryFake());
        var notification = CreateNotification(subscriptionId, tenantId, "valid-client-state");

        // When
        await service.HandleNotificationsAsync(
            [notification, notification],
            CancellationToken.None);

        // Then
        Assert.Single(subscription.Microsoft365Source.Synchronizations);
    }

    private static MicrosoftGraphNotificationService CreateService(
        SubscriptionRepositoryFake repository,
        IndexedContentRepositoryFake indexedContentRepository) =>
        new(
            repository,
            indexedContentRepository,
            new ClientStateProtectorFake(),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-08-21T12:00:00Z")));

    private sealed class IndexedContentRepositoryFake : IMicrosoft365IndexedContentRepository
    {
        public Guid? RequestedSourceId { get; private set; }

        public Task<Microsoft365IndexedContent?> FindAsync(
            Guid organizationId,
            Guid sourceId,
            string externalContentId,
            CancellationToken cancellationToken = default) => Task.FromResult<Microsoft365IndexedContent?>(null);

        public Task<IReadOnlyCollection<Microsoft365IndexedContent>> GetAclReconciliationCandidatesAsync(
            DateTimeOffset dueAt,
            int maximumResults,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365IndexedContent>>([]);

        public Task RequestAclReconciliationAsync(
            Guid sourceId,
            DateTimeOffset dueAt,
            CancellationToken cancellationToken = default)
        {
            RequestedSourceId = sourceId;
            return Task.CompletedTask;
        }

        public Task SaveAsync(
            Microsoft365IndexedContent content,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static MicrosoftGraphNotification CreateNotification(
        string subscriptionId,
        string tenantId,
        string clientState) =>
        new(subscriptionId, clientState, tenantId, Resource: null);

    private static Microsoft365Subscription CreateListSubscription(
        Guid organizationId,
        string subscriptionId,
        string tenantId,
        string siteId,
        string listId) =>
        CreateSubscription(
            organizationId,
            subscriptionId,
            tenantId,
            new Microsoft365List
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SiteId = siteId,
                ListId = listId,
                Kind = Microsoft365SourceKind.SharePointList,
                Status = Microsoft365SourceStatus.Enabled,
                IsIndexed = true
            });

    private static Microsoft365Subscription CreateDriveSubscription(
        Guid organizationId,
        string subscriptionId,
        string tenantId,
        string siteId,
        string driveId) =>
        CreateSubscription(
            organizationId,
            subscriptionId,
            tenantId,
            new Microsoft365Drive
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SiteId = siteId,
                DriveId = driveId,
                Kind = Microsoft365SourceKind.SharePointDrive,
                Status = Microsoft365SourceStatus.Enabled,
                IsIndexed = true
            });

    private static Microsoft365Subscription CreateSubscription(
        Guid organizationId,
        string subscriptionId,
        string tenantId,
        Microsoft365Source source)
    {
        var connector = new OrganizationConnector
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Status = RecordStatus.Active,
            Type = ConnectorType.Microsoft365
        };
        var connection = new Microsoft365Connection
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OrganizationConnectorId = connector.Id,
            TenantId = tenantId,
            Status = Microsoft365ConnectionStatus.Active,
            OrganizationConnector = connector
        };
        source.Microsoft365ConnectionId = connection.Id;
        source.Microsoft365Connection = connection;
        var subscription = new Microsoft365Subscription
        {
            Id = Guid.NewGuid(),
            Microsoft365SourceId = source.Id,
            OrganizationId = organizationId,
            Resource = source is Microsoft365List list
                ? $"/sites/{list.SiteId}/lists/{list.ListId}"
                : $"/drives/{((Microsoft365Drive)source).DriveId}/root",
            MicrosoftSubscriptionId = subscriptionId,
            ProtectedClientState = "protected-client-state",
            ExpiresAt = DateTimeOffset.Parse("2026-08-22T12:00:00Z"),
            Status = Microsoft365SubscriptionStatus.Active,
            Microsoft365Source = source
        };
        source.Subscriptions.Add(subscription);
        return subscription;
    }

    private sealed class ClientStateProtectorFake : IMicrosoft365ClientStateProtector
    {
        public Microsoft365ClientState Create() =>
            new("valid-client-state", "protected-client-state");

        public bool Matches(string clientState, string protectedClientState) =>
            clientState == "valid-client-state"
            && protectedClientState == "protected-client-state";
    }

    private sealed class SubscriptionRepositoryFake(Microsoft365Subscription subscription)
        : IMicrosoft365SubscriptionRepository
    {
        public Task<IReadOnlyCollection<Microsoft365Subscription>> GetMaintenanceCandidatesAsync(
            DateTimeOffset renewBefore,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365Subscription>>([]);

        public Task<IReadOnlyCollection<Microsoft365Subscription>> GetReconciliationCandidatesAsync(
            DateTimeOffset dueAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365Subscription>>([]);

        public Task<Microsoft365Subscription?> FindActiveForNotificationAsync(
            string microsoftSubscriptionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Microsoft365Subscription?>(
                subscription.MicrosoftSubscriptionId == microsoftSubscriptionId
                    ? subscription
                    : null);

        public Microsoft365Synchronization GetOrCreateDeltaSynchronization(
            Microsoft365Subscription candidate,
            DateTimeOffset requestedAt)
        {
            var synchronization = candidate.Microsoft365Source.Synchronizations.SingleOrDefault();
            if (synchronization is not null)
            {
                return synchronization;
            }

            synchronization = new Microsoft365Synchronization
            {
                Id = Guid.NewGuid(),
                Microsoft365SourceId = candidate.Microsoft365SourceId,
                Type = Microsoft365SynchronizationType.Delta,
                Status = Microsoft365SynchronizationStatus.Pending,
                RequestedAt = requestedAt
            };
            candidate.Microsoft365Source.Synchronizations.Add(synchronization);
            return synchronization;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
