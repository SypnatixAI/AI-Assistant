using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ListConsultationServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnOwnedSiteAndAdministrator_When_GetListsAsync_Then_ReturnsOrganizationSiteLists(
        Organization organization,
        OrganizationMember admin,
        Microsoft365Site site,
        Microsoft365List list,
        CancellationToken cancellationToken)
    {
        // Given
        admin.OrganizationId = organization.Id;
        admin.Role = OrganizationRole.Admin;
        site.OrganizationId = organization.Id;
        list.OrganizationId = organization.Id;
        list.SiteId = site.SiteId;
        var repository = new StubSourceDiscoveryRepository { Site = site, Lists = [list] };
        var service = new Microsoft365ListConsultationService(
            new StubAuthenticateUserService { Result = (organization, admin) },
            repository);

        // When
        var result = await service.GetListsAsync(site.SiteId, cancellationToken);

        // Then
        Assert.Same(repository.Lists, result);
        Assert.Equal(organization.Id, repository.ReceivedOrganizationId);
        Assert.Equal(site.SiteId, repository.ReceivedSiteId);
        Assert.Equal(cancellationToken, repository.ReceivedCancellationToken);
        Assert.Equal(1, repository.GetListsCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_ANonAdministrator_When_GetListsAsync_Then_ThrowsForbiddenWithoutQueryingRepository(
        Organization organization,
        OrganizationMember member,
        string siteId)
    {
        // Given
        member.OrganizationId = organization.Id;
        member.Role = OrganizationRole.User;
        var repository = new StubSourceDiscoveryRepository();
        var service = new Microsoft365ListConsultationService(
            new StubAuthenticateUserService { Result = (organization, member) },
            repository);

        // When
        var action = () => service.GetListsAsync(siteId, CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<ForbiddenException>(action);
        Assert.Equal(0, repository.FindSiteCallCount);
        Assert.Equal(0, repository.GetListsCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AForeignOrUnknownSite_When_GetListsAsync_Then_ThrowsNotFoundWithoutQueryingLists(
        Organization organization,
        OrganizationMember admin,
        string siteId)
    {
        // Given
        admin.OrganizationId = organization.Id;
        admin.Role = OrganizationRole.Admin;
        var repository = new StubSourceDiscoveryRepository();
        var service = new Microsoft365ListConsultationService(
            new StubAuthenticateUserService { Result = (organization, admin) },
            repository);

        // When
        var action = () => service.GetListsAsync(siteId, CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<NotFoundException>(action);
        Assert.Equal(organization.Id, repository.ReceivedOrganizationId);
        Assert.Equal(siteId, repository.ReceivedSiteId);
        Assert.Equal(0, repository.GetListsCallCount);
    }

    private sealed class StubSourceDiscoveryRepository : IMicrosoft365SourceDiscoveryRepository
    {
        public Microsoft365Site? Site { get; init; }
        public IReadOnlyCollection<Microsoft365List> Lists { get; init; } = [];
        public Guid ReceivedOrganizationId { get; private set; }
        public string? ReceivedSiteId { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }
        public int FindSiteCallCount { get; private set; }
        public int GetListsCallCount { get; private set; }

        public Task<Microsoft365Site?> FindSiteAsync(
            Guid organizationId,
            string siteId,
            CancellationToken cancellationToken = default)
        {
            FindSiteCallCount++;
            RecordCall(organizationId, siteId, cancellationToken);
            return Task.FromResult(Site);
        }

        public Task<IReadOnlyCollection<Microsoft365List>> GetListsAsync(
            Guid organizationId,
            string siteId,
            CancellationToken cancellationToken = default)
        {
            GetListsCallCount++;
            RecordCall(organizationId, siteId, cancellationToken);
            return Task.FromResult(Lists);
        }

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
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private void RecordCall(
            Guid organizationId,
            string siteId,
            CancellationToken cancellationToken)
        {
            ReceivedOrganizationId = organizationId;
            ReceivedSiteId = siteId;
            ReceivedCancellationToken = cancellationToken;
        }
    }

    private sealed class StubAuthenticateUserService : IAuthenticateUserService
    {
        public required (Organization Organization, OrganizationMember Member) Result { get; init; }

        public Task<(Organization Organization, OrganizationMember Member)> GetOrganizationAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }
}
