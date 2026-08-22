using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ListActivationServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_ADiscoveredList_When_SetIndexingAsync_Then_InitialSynchronizationAndSubscriptionAreRequested(
        Guid organizationId,
        Guid connectionId,
        Guid connectorId,
        string siteId,
        string listId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Given
        var site = CreateSite(organizationId, connectionId, connectorId, siteId);
        var list = CreateList(organizationId, connectionId, connectorId, siteId, listId);
        var repository = new StubSourceDiscoveryRepository { Site = site, List = list };
        var service = CreateService(organizationId, OrganizationRole.Admin, repository, now);

        // When
        var result = await service.SetIndexingAsync(siteId, listId, true, cancellationToken);

        // Then
        Assert.Same(list, result);
        Assert.True(list.IsIndexed);
        Assert.Equal(Microsoft365SourceStatus.Enabled, list.Status);
        Assert.Equal(now, list.EnabledAt);
        Assert.Equal(1, repository.SaveActivationCallCount);
        Assert.Equal(now, repository.ReceivedRequestedAt);
        Assert.Equal(cancellationToken, repository.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEnabledList_When_SetIndexingAsync_Then_NoSecondUsefulWorkIsRequested(
        Guid organizationId,
        Guid connectionId,
        Guid connectorId,
        string siteId,
        string listId,
        DateTimeOffset now)
    {
        // Given
        var site = CreateSite(organizationId, connectionId, connectorId, siteId);
        var list = CreateList(organizationId, connectionId, connectorId, siteId, listId);
        list.EnableIndexing(now.AddMinutes(-1));
        var repository = new StubSourceDiscoveryRepository { Site = site, List = list };
        var service = CreateService(organizationId, OrganizationRole.Admin, repository, now);

        // When
        var result = await service.SetIndexingAsync(siteId, listId, true, CancellationToken.None);

        // Then
        Assert.Same(list, result);
        Assert.Equal(0, repository.SaveActivationCallCount);
        Assert.Equal(now.AddMinutes(-1), list.EnabledAt);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEnabledList_When_SetIndexingAsync_Then_DisablesAndRequestsCleanup(
        Guid organizationId,
        Guid connectionId,
        Guid connectorId,
        string siteId,
        string listId,
        DateTimeOffset now)
    {
        // Given
        var site = CreateSite(organizationId, connectionId, connectorId, siteId);
        var list = CreateList(organizationId, connectionId, connectorId, siteId, listId);
        list.EnableIndexing(now.AddMinutes(-1));
        var repository = new StubSourceDiscoveryRepository { Site = site, List = list };
        var service = CreateService(organizationId, OrganizationRole.Admin, repository, now);

        // When
        var result = await service.SetIndexingAsync(siteId, listId, false, CancellationToken.None);

        // Then
        Assert.Same(list, result);
        Assert.False(list.IsIndexed);
        Assert.Equal(Microsoft365SourceStatus.Disabled, list.Status);
        Assert.Equal(1, repository.SaveDeactivationCallCount);
        Assert.True(repository.ReceivedRequestIndexCleanup);
    }

    [Theory, AutoDomainData]
    public async Task Given_ANonAdministrator_When_SetIndexingAsync_Then_ThrowsForbiddenWithoutQueryingSources(
        Guid organizationId,
        string siteId,
        string listId,
        DateTimeOffset now)
    {
        // Given
        var repository = new StubSourceDiscoveryRepository();
        var service = CreateService(organizationId, OrganizationRole.User, repository, now);

        // When
        var action = () => service.SetIndexingAsync(siteId, listId, true, CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<ForbiddenException>(action);
        Assert.Equal(0, repository.FindSiteCallCount);
        Assert.Equal(0, repository.FindListCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AForeignOrUnknownList_When_SetIndexingAsync_Then_ThrowsNotFoundWithoutSaving(
        Guid organizationId,
        Guid connectionId,
        Guid connectorId,
        string siteId,
        string listId,
        DateTimeOffset now)
    {
        // Given
        var repository = new StubSourceDiscoveryRepository
        {
            Site = CreateSite(organizationId, connectionId, connectorId, siteId)
        };
        var service = CreateService(organizationId, OrganizationRole.Admin, repository, now);

        // When
        var action = () => service.SetIndexingAsync(siteId, listId, true, CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<NotFoundException>(action);
        Assert.Equal(0, repository.SaveActivationCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnAlreadyDisabledList_When_SetIndexingAsync_Then_NoSecondCleanupIsRequested(
        Guid organizationId,
        Guid connectionId,
        Guid connectorId,
        string siteId,
        string listId,
        DateTimeOffset now)
    {
        // Given
        var site = CreateSite(organizationId, connectionId, connectorId, siteId);
        var list = CreateList(organizationId, connectionId, connectorId, siteId, listId);
        list.DisableIndexing();
        var repository = new StubSourceDiscoveryRepository { Site = site, List = list };
        var service = CreateService(organizationId, OrganizationRole.Admin, repository, now);

        // When
        var result = await service.SetIndexingAsync(siteId, listId, false, CancellationToken.None);

        // Then
        Assert.Same(list, result);
        Assert.Equal(0, repository.SaveDeactivationCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_ClientContentAndSecrets_When_SetIndexingAsync_Then_LogsOnlyTechnicalContext(
        Guid organizationId,
        Guid connectionId,
        Guid connectorId,
        string siteId,
        string listId,
        string clientContent,
        string protectedClientState,
        DateTimeOffset now)
    {
        // Given
        var site = CreateSite(organizationId, connectionId, connectorId, siteId);
        var list = CreateList(organizationId, connectionId, connectorId, siteId, listId);
        list.DisplayName = clientContent;
        list.EnableIndexing(now.AddMinutes(-1));
        list.Subscriptions.Add(new Microsoft365Subscription
        {
            ProtectedClientState = protectedClientState,
            Status = Microsoft365SubscriptionStatus.Active
        });
        var logger = new RecordingLogger<Microsoft365ListActivationService>();
        var service = CreateService(
            organizationId,
            OrganizationRole.Admin,
            new StubSourceDiscoveryRepository { Site = site, List = list },
            now,
            logger);

        // When
        await service.SetIndexingAsync(siteId, listId, false, CancellationToken.None);

        // Then
        var log = Assert.Single(logger.Messages);
        Assert.Contains(organizationId.ToString(), log, StringComparison.Ordinal);
        Assert.Contains(siteId, log, StringComparison.Ordinal);
        Assert.Contains(listId, log, StringComparison.Ordinal);
        Assert.DoesNotContain(clientContent, log, StringComparison.Ordinal);
        Assert.DoesNotContain(protectedClientState, log, StringComparison.Ordinal);
    }

    private static Microsoft365ListActivationService CreateService(
        Guid organizationId,
        OrganizationRole role,
        StubSourceDiscoveryRepository repository,
        DateTimeOffset now,
        ILogger<Microsoft365ListActivationService>? logger = null) =>
        new(
            new StubAuthenticateUserService(organizationId, role),
            repository,
            new FixedTimeProvider(now),
            logger ?? NullLogger<Microsoft365ListActivationService>.Instance);

    private static Microsoft365Site CreateSite(
        Guid organizationId,
        Guid connectionId,
        Guid connectorId,
        string siteId)
    {
        var connector = new OrganizationConnector
        {
            Id = connectorId,
            OrganizationId = organizationId,
            Type = ConnectorType.Microsoft365,
            Status = RecordStatus.Active,
            IsConfigured = true
        };
        var connection = new Microsoft365Connection
        {
            Id = connectionId,
            OrganizationId = organizationId,
            OrganizationConnectorId = connectorId,
            Status = Microsoft365ConnectionStatus.Active,
            OrganizationConnector = connector
        };
        return new Microsoft365Site
        {
            OrganizationId = organizationId,
            OrganizationConnectorId = connectorId,
            Microsoft365ConnectionId = connectionId,
            SiteId = siteId,
            Status = Microsoft365SourceStatus.Enabled,
            OrganizationConnector = connector,
            Microsoft365Connection = connection
        };
    }

    private static Microsoft365List CreateList(
        Guid organizationId,
        Guid connectionId,
        Guid connectorId,
        string siteId,
        string listId) =>
        new()
        {
            OrganizationId = organizationId,
            OrganizationConnectorId = connectorId,
            Microsoft365ConnectionId = connectionId,
            SiteId = siteId,
            ListId = listId,
            Status = Microsoft365SourceStatus.Discovered,
            IsIndexed = false
        };

    private sealed class StubAuthenticateUserService(
        Guid organizationId,
        OrganizationRole role) : IAuthenticateUserService
    {
        public Task<(Organization Organization, OrganizationMember Member)> GetOrganizationAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult((
                new Organization { Id = organizationId },
                new OrganizationMember { OrganizationId = organizationId, Role = role }));
    }

    private sealed class StubSourceDiscoveryRepository : IMicrosoft365SourceDiscoveryRepository
    {
        public Microsoft365Site? Site { get; init; }
        public Microsoft365List? List { get; init; }
        public int FindSiteCallCount { get; private set; }
        public int FindListCallCount { get; private set; }
        public int SaveActivationCallCount { get; private set; }
        public int SaveDeactivationCallCount { get; private set; }
        public bool ReceivedRequestIndexCleanup { get; private set; }
        public DateTimeOffset ReceivedRequestedAt { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<Microsoft365Site?> FindSiteAsync(
            Guid organizationId,
            string siteId,
            CancellationToken cancellationToken = default)
        {
            FindSiteCallCount++;
            return Task.FromResult(Site);
        }

        public Task<IReadOnlyCollection<Microsoft365List>> GetListsAsync(
            Guid organizationId,
            string siteId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365List>>([]);

        public Task<Microsoft365List?> FindListAsync(
            Guid organizationId,
            string siteId,
            string listId,
            CancellationToken cancellationToken = default)
        {
            FindListCallCount++;
            return Task.FromResult(List);
        }

        public Task<Microsoft365ListIndexingRequestCounts> SaveListActivationAsync(
            Microsoft365List list,
            DateTimeOffset requestedAt,
            CancellationToken cancellationToken = default)
        {
            SaveActivationCallCount++;
            ReceivedRequestedAt = requestedAt;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(new Microsoft365ListIndexingRequestCounts(
                InitialSynchronizationRequests: 1,
                CancelledIngestionJobs: 0,
                SubscriptionCreationRequests: 1,
                SubscriptionStopRequests: 0,
                IndexCleanupRequests: 0));
        }

        public Task<Microsoft365ListIndexingRequestCounts> SaveListDeactivationAsync(
            Microsoft365List list,
            DateTimeOffset requestedAt,
            bool requestIndexCleanup,
            CancellationToken cancellationToken = default)
        {
            SaveDeactivationCallCount++;
            ReceivedRequestedAt = requestedAt;
            ReceivedRequestIndexCleanup = requestIndexCleanup;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(new Microsoft365ListIndexingRequestCounts(
                InitialSynchronizationRequests: 0,
                CancelledIngestionJobs: 1,
                SubscriptionCreationRequests: 0,
                SubscriptionStopRequests: 1,
                IndexCleanupRequests: requestIndexCleanup ? 1 : 0));
        }

        public Task ReconcileSiteSourcesAsync(
            Microsoft365Site site,
            IReadOnlyCollection<Microsoft365SourceDiscoveryData> drives,
            IReadOnlyCollection<Microsoft365SourceDiscoveryData> lists,
            DateTimeOffset discoveredAt,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
