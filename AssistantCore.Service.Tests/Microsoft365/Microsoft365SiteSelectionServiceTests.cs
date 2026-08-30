using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SiteSelectionServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_CompatibleSiteContents_When_SelectAsync_Then_ActivatesEveryDiscoveredContent(
        string siteId,
        string driveId,
        string listId,
        Microsoft365SiteResponse site,
        CancellationToken cancellationToken)
    {
        // Given
        var driveService = new StubDriveAdministrationService { Site = site };
        var discoveryService = new StubSiteSourcesDiscoveryService
        {
            Result = Microsoft365SiteSourcesDiscoveryResult.Succeeded(
                new Microsoft365DiscoveredSiteSources(
                    [new Microsoft365DiscoveredDrive(siteId, driveId, "Documents", null)],
                    [new Microsoft365DiscoveredList(siteId, listId, "Requests", null)]))
        };
        var listService = new StubListActivationService();
        var service = new Microsoft365SiteSelectionService(
            driveService,
            discoveryService,
            listService);

        // When
        var result = await service.SelectAsync(siteId, cancellationToken);

        // Then
        Assert.Same(site, result);
        Assert.Equal(siteId, driveService.RegisteredSiteId);
        Assert.Equal((siteId, driveId, true), driveService.ActivatedDrive);
        Assert.Equal(siteId, discoveryService.ReceivedSiteId);
        Assert.Equal((siteId, listId, true), listService.ActivatedList);
        Assert.Equal(cancellationToken, driveService.ReceivedCancellationToken);
        Assert.Equal(cancellationToken, discoveryService.ReceivedCancellationToken);
        Assert.Equal(cancellationToken, listService.ReceivedCancellationToken);
    }

    [Theory, InlineAutoDomainData(Microsoft365SiteSourcesDiscoveryStatus.Forbidden)]
    public async Task Given_SiteContentsCannotBeDiscovered_When_SelectAsync_Then_ThrowsWithoutActivatingContent(
        Microsoft365SiteSourcesDiscoveryStatus discoveryStatus,
        string siteId,
        Microsoft365SiteResponse site,
        CancellationToken cancellationToken)
    {
        // Given
        var driveService = new StubDriveAdministrationService { Site = site };
        var discoveryService = new StubSiteSourcesDiscoveryService
        {
            Result = new Microsoft365SiteSourcesDiscoveryResult(
                discoveryStatus,
                new Microsoft365DiscoveredSiteSources([], []))
        };
        var listService = new StubListActivationService();
        var service = new Microsoft365SiteSelectionService(
            driveService,
            discoveryService,
            listService);

        // When
        var action = () => service.SelectAsync(siteId, cancellationToken);

        // Then
        await Assert.ThrowsAsync<ForbiddenException>(action);
        Assert.Null(driveService.ActivatedDrive);
        Assert.Null(listService.ActivatedList);
    }

    private sealed class StubDriveAdministrationService : IMicrosoft365DriveAdministrationService
    {
        public required Microsoft365SiteResponse Site { get; init; }
        public string? RegisteredSiteId { get; private set; }
        public (string SiteId, string DriveId, bool IsIndexed)? ActivatedDrive { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<Microsoft365SiteResponse> RegisterSiteAsync(
            string siteId,
            CancellationToken cancellationToken = default)
        {
            RegisteredSiteId = siteId;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(Site);
        }

        public Task<IReadOnlyCollection<Microsoft365DriveResponse>> GetDrivesAsync(
            string siteId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365DriveResponse>>([]);

        public Task<Microsoft365DriveResponse> EnableDriveAsync(
            string siteId,
            string driveId,
            bool isIndexed,
            CancellationToken cancellationToken = default)
        {
            ActivatedDrive = (siteId, driveId, isIndexed);
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(new Microsoft365DriveResponse(
                siteId,
                driveId,
                "Documents",
                null,
                "Enabled",
                isIndexed));
        }
    }

    private sealed class StubSiteSourcesDiscoveryService : IMicrosoft365SiteSourcesDiscoveryService
    {
        public required Microsoft365SiteSourcesDiscoveryResult Result { get; init; }
        public string? ReceivedSiteId { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<Microsoft365SiteSourcesDiscoveryResult> DiscoverAsync(
            string siteId,
            CancellationToken cancellationToken = default)
        {
            ReceivedSiteId = siteId;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubListActivationService : IMicrosoft365ListActivationService
    {
        public (string SiteId, string ListId, bool IsIndexed)? ActivatedList { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<Microsoft365List> SetIndexingAsync(
            string siteId,
            string listId,
            bool isIndexed,
            CancellationToken cancellationToken = default)
        {
            ActivatedList = (siteId, listId, isIndexed);
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(new Microsoft365List
            {
                SiteId = siteId,
                ListId = listId,
                IsIndexed = isIndexed
            });
        }
    }
}
