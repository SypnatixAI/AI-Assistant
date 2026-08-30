using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SiteSourcesDiscoveryServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnActiveOwnedSite_When_DiscoverAsync_Then_ReconcilesDiscoveredSources(
        Guid organizationId,
        Guid connectionId,
        Guid connectorId,
        string siteId,
        string accessToken,
        DateTimeOffset now)
    {
        // Given
        var site = CreateSite(organizationId, connectionId, connectorId, siteId);
        var repository = new StubSourceDiscoveryRepository { Site = site };
        var sourceClient = new StubSiteSourcesClient
        {
            Result = Microsoft365SiteSourcesDiscoveryResult.Succeeded(
                new Microsoft365DiscoveredSiteSources(
                    [new Microsoft365DiscoveredDrive(siteId, "drive-id", "Documents", null)],
                    [new Microsoft365DiscoveredList(siteId, "list-id", "Requests", null)]))
        };
        var service = CreateService(
            organizationId,
            repository,
            sourceClient,
            new StubTokenStore { AccessToken = accessToken },
            now);

        // When
        var result = await service.DiscoverAsync(siteId, CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365SiteSourcesDiscoveryStatus.Succeeded, result.Status);
        Assert.Equal(organizationId, repository.ReceivedOrganizationId);
        Assert.Equal(siteId, repository.ReceivedSiteId);
        Assert.Equal(siteId, sourceClient.ReceivedSiteId);
        Assert.Equal(accessToken, sourceClient.ReceivedAccessToken);
        Assert.Equal("drive-id", Assert.Single(repository.ReceivedDrives).MicrosoftResourceId);
        Assert.Equal("list-id", Assert.Single(repository.ReceivedLists).MicrosoftResourceId);
        Assert.Equal(now, repository.ReceivedDiscoveredAt);
    }

    [Theory, AutoDomainData]
    public async Task Given_AForeignOrUnknownSite_When_DiscoverAsync_Then_ThrowsNotFoundWithoutCallingGraph(
        Guid organizationId,
        string siteId,
        DateTimeOffset now)
    {
        // Given
        var repository = new StubSourceDiscoveryRepository();
        var sourceClient = new StubSiteSourcesClient();
        var service = CreateService(
            organizationId,
            repository,
            sourceClient,
            new StubTokenStore { AccessToken = "token" },
            now);

        // When
        var action = () => service.DiscoverAsync(siteId, CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<NotFoundException>(action);
        Assert.Equal(organizationId, repository.ReceivedOrganizationId);
        Assert.Equal(0, sourceClient.CallCount);
        Assert.Equal(0, repository.ReconcileCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AThrottledDiscovery_When_DiscoverAsync_Then_ReturnsResultWithoutChangingSources(
        Guid organizationId,
        Guid connectionId,
        Guid connectorId,
        string siteId,
        TimeSpan retryAfter,
        DateTimeOffset now)
    {
        // Given
        var repository = new StubSourceDiscoveryRepository
        {
            Site = CreateSite(organizationId, connectionId, connectorId, siteId)
        };
        var sourceClient = new StubSiteSourcesClient
        {
            Result = Microsoft365SiteSourcesDiscoveryResult.Throttled(retryAfter, null)
        };
        var service = CreateService(
            organizationId,
            repository,
            sourceClient,
            new StubTokenStore { AccessToken = "token" },
            now);

        // When
        var result = await service.DiscoverAsync(siteId, CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365SiteSourcesDiscoveryStatus.Throttled, result.Status);
        Assert.Equal(retryAfter, result.RetryAfterDelay);
        Assert.Equal(0, repository.ReconcileCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInactiveConnector_When_DiscoverAsync_Then_ThrowsBadRequestWithoutCallingGraph(
        Guid organizationId,
        Guid connectionId,
        Guid connectorId,
        string siteId,
        DateTimeOffset now)
    {
        // Given
        var site = CreateSite(organizationId, connectionId, connectorId, siteId);
        site.OrganizationConnector.Status = RecordStatus.Inactive;
        var repository = new StubSourceDiscoveryRepository { Site = site };
        var sourceClient = new StubSiteSourcesClient();
        var service = CreateService(
            organizationId,
            repository,
            sourceClient,
            new StubTokenStore { AccessToken = "token" },
            now);

        // When
        var action = () => service.DiscoverAsync(siteId, CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<BadRequestException>(action);
        Assert.Equal(0, sourceClient.CallCount);
    }

    private static Microsoft365SiteSourcesDiscoveryService CreateService(
        Guid organizationId,
        StubSourceDiscoveryRepository repository,
        StubSiteSourcesClient sourceClient,
        StubTokenStore tokenStore,
        DateTimeOffset now) =>
        new(
            new StubAuthenticateUserService(organizationId),
            repository,
            sourceClient,
            tokenStore,
            new FixedTimeProvider(now));

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

    private sealed class StubAuthenticateUserService(Guid organizationId) : IAuthenticateUserService
    {
        public Task<(Organization Organization, OrganizationMember Member)> GetOrganizationAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult((
                new Organization { Id = organizationId },
                new OrganizationMember { OrganizationId = organizationId, Role = OrganizationRole.Admin }));
    }

    private sealed class StubSourceDiscoveryRepository : IMicrosoft365SourceDiscoveryRepository
    {
        public Microsoft365Site? Site { get; init; }
        public Guid ReceivedOrganizationId { get; private set; }
        public string? ReceivedSiteId { get; private set; }
        public IReadOnlyCollection<Microsoft365SourceDiscoveryData> ReceivedDrives { get; private set; } = [];
        public IReadOnlyCollection<Microsoft365SourceDiscoveryData> ReceivedLists { get; private set; } = [];
        public DateTimeOffset ReceivedDiscoveredAt { get; private set; }
        public int ReconcileCallCount { get; private set; }

        public Task<IReadOnlyCollection<string>> GetSiteIdsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<Microsoft365Site?> FindSiteAsync(
            Guid organizationId,
            string siteId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrganizationId = organizationId;
            ReceivedSiteId = siteId;
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
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Microsoft365List?>(null);

        public Task<Microsoft365ListIndexingRequestCounts> SaveListActivationAsync(
            Microsoft365List list,
            DateTimeOffset requestedAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Microsoft365ListIndexingRequestCounts.Empty);

        public Task<Microsoft365ListIndexingRequestCounts> SaveListDeactivationAsync(
            Microsoft365List list,
            DateTimeOffset requestedAt,
            bool requestIndexCleanup,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Microsoft365ListIndexingRequestCounts.Empty);

        public Task ReconcileSiteSourcesAsync(
            Microsoft365Site site,
            IReadOnlyCollection<Microsoft365SourceDiscoveryData> drives,
            IReadOnlyCollection<Microsoft365SourceDiscoveryData> lists,
            DateTimeOffset discoveredAt,
            CancellationToken cancellationToken = default)
        {
            ReconcileCallCount++;
            ReceivedDrives = drives;
            ReceivedLists = lists;
            ReceivedDiscoveredAt = discoveredAt;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSiteSourcesClient : IMicrosoft365SiteSourcesClient
    {
        public Microsoft365SiteSourcesDiscoveryResult Result { get; init; } =
            Microsoft365SiteSourcesDiscoveryResult.Forbidden();
        public string? ReceivedAccessToken { get; private set; }
        public string? ReceivedSiteId { get; private set; }
        public int CallCount { get; private set; }

        public Task<Microsoft365SiteSourcesDiscoveryResult> GetSiteSourcesAsync(
            string accessToken,
            string siteId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedAccessToken = accessToken;
            ReceivedSiteId = siteId;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubTokenStore : IMicrosoft365TechnicalTokenStore
    {
        public string? AccessToken { get; init; }

        public Task StoreAsync(Guid connectionId, string accessToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetAsync(Guid connectionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(AccessToken);

        public Task RemoveAsync(Guid connectionId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
