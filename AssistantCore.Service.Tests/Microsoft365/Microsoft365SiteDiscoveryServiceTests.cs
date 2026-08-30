using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SiteDiscoveryServiceTests
{
    [Theory, InlineAutoDomainData("selected-site", "available-site")]
    public async Task Given_AnActiveConnection_When_GetAvailableSitesAsync_Then_ReturnsSortedSitesWithSelectionState(
        string selectedSiteId,
        string availableSiteId,
        Guid organizationId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Given
        var authenticationService = new StubAuthenticateUserService(
            organizationId,
            OrganizationRole.Admin);
        var connectionRepository = new StubConnectionRepository
        {
            ActiveConnection = new Microsoft365Connection
            {
                OrganizationId = organizationId,
                TenantId = tenantId.ToString("D"),
                Status = Microsoft365ConnectionStatus.Active
            }
        };
        var sourceRepository = new StubSourceDiscoveryRepository
        {
            SiteIds = [selectedSiteId]
        };
        var siteClient = new StubSiteClient
        {
            Sites =
            [
                new Microsoft365AvailableSite(availableSiteId, "Operations", "https://contoso.test/operations"),
                new Microsoft365AvailableSite(selectedSiteId, "Finance", "https://contoso.test/finance")
            ]
        };
        var service = new Microsoft365SiteDiscoveryService(
            authenticationService,
            connectionRepository,
            sourceRepository,
            siteClient);

        // When
        var sites = await service.GetAvailableSitesAsync(cancellationToken);

        // Then
        Assert.Collection(
            sites,
            site =>
            {
                Assert.Equal(selectedSiteId, site.SiteId);
                Assert.True(site.IsSelected);
            },
            site =>
            {
                Assert.Equal(availableSiteId, site.SiteId);
                Assert.False(site.IsSelected);
            });
        Assert.Equal(organizationId, sourceRepository.ReceivedOrganizationId);
        Assert.Equal(tenantId.ToString("D"), siteClient.ReceivedTenantId);
        Assert.Equal(cancellationToken, siteClient.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_ARegularMember_When_GetAvailableSitesAsync_Then_AccessIsForbidden(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        // Given
        var siteClient = new StubSiteClient();
        var service = new Microsoft365SiteDiscoveryService(
            new StubAuthenticateUserService(organizationId, OrganizationRole.User),
            new StubConnectionRepository(),
            new StubSourceDiscoveryRepository(),
            siteClient);

        // When
        var action = () => service.GetAvailableSitesAsync(cancellationToken);

        // Then
        await Assert.ThrowsAsync<ForbiddenException>(action);
        Assert.Null(siteClient.ReceivedTenantId);
    }

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

    private sealed class StubConnectionRepository : IMicrosoft365ConnectionRepository
    {
        public Microsoft365Connection? ActiveConnection { get; init; }

        public Task<Microsoft365Connection?> FindActiveByOrganizationAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ActiveConnection);

        public Task<Microsoft365Connection> PrepareConsentAsync(Guid organizationId, string stateHash, DateTimeOffset stateExpiresAt, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Microsoft365Connection());
        public Task<Microsoft365Connection?> FindConsentAsync(Guid organizationId, string stateHash, CancellationToken cancellationToken = default) =>
            Task.FromResult<Microsoft365Connection?>(null);
        public Task<bool> IsTenantConnectedToAnotherOrganizationAsync(Guid organizationId, string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
        public Task<Microsoft365Connection?> FindByIdAsync(Guid connectionId, Guid organizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Microsoft365Connection?>(null);
        public Task<Microsoft365Connection?> FindForProcessingAsync(Guid connectionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Microsoft365Connection?>(null);
        public Task CompleteConsentAsync(Microsoft365Connection connection, string tenantId, DateTimeOffset completedAt, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task MarkConsentErrorAsync(Microsoft365Connection connection, string errorCode, DateTimeOffset occurredAt, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task RevokeAsync(Microsoft365Connection connection, DateTimeOffset occurredAt, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubSourceDiscoveryRepository : IMicrosoft365SourceDiscoveryRepository
    {
        public IReadOnlyCollection<string> SiteIds { get; init; } = [];
        public Guid ReceivedOrganizationId { get; private set; }

        public Task<IReadOnlyCollection<string>> GetSiteIdsAsync(Guid organizationId, CancellationToken cancellationToken = default)
        {
            ReceivedOrganizationId = organizationId;
            return Task.FromResult(SiteIds);
        }

        public Task<Microsoft365Site?> FindSiteAsync(Guid organizationId, string siteId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Microsoft365Site?>(null);
        public Task<IReadOnlyCollection<Microsoft365List>> GetListsAsync(Guid organizationId, string siteId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365List>>([]);
        public Task<Microsoft365List?> FindListAsync(Guid organizationId, string siteId, string listId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Microsoft365List?>(null);
        public Task<Microsoft365ListIndexingRequestCounts> SaveListActivationAsync(Microsoft365List list, DateTimeOffset requestedAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(Microsoft365ListIndexingRequestCounts.Empty);
        public Task<Microsoft365ListIndexingRequestCounts> SaveListDeactivationAsync(Microsoft365List list, DateTimeOffset requestedAt, bool requestIndexCleanup, CancellationToken cancellationToken = default) =>
            Task.FromResult(Microsoft365ListIndexingRequestCounts.Empty);
        public Task ReconcileSiteSourcesAsync(Microsoft365Site site, IReadOnlyCollection<Microsoft365SourceDiscoveryData> drives, IReadOnlyCollection<Microsoft365SourceDiscoveryData> lists, DateTimeOffset discoveredAt, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubSiteClient : IMicrosoft365SiteClient
    {
        public IReadOnlyCollection<Microsoft365AvailableSite> Sites { get; init; } = [];
        public string? ReceivedTenantId { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<IReadOnlyCollection<Microsoft365AvailableSite>> ListAsync(
            string tenantId,
            CancellationToken cancellationToken = default)
        {
            ReceivedTenantId = tenantId;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(Sites);
        }

        public Task<(string SiteId, string DisplayName, string WebUrl)> GetAsync(
            string tenantId,
            string siteId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((siteId, "Site", "https://contoso.test/site"));
    }
}
