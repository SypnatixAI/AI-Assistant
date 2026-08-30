using AssistantCore.Repository.Abstractions;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365SiteSelectionService(
    IMicrosoft365DriveAdministrationService driveAdministrationService,
    IMicrosoft365SiteSourcesDiscoveryService siteSourcesDiscoveryService,
    IMicrosoft365ListActivationService listActivationService)
    : IMicrosoft365SiteSelectionService
{
    public async Task<Microsoft365SiteResponse> SelectAsync(
        string siteId,
        CancellationToken cancellationToken = default)
    {
        var site = await driveAdministrationService.RegisterSiteAsync(
            siteId,
            cancellationToken);
        var discovery = await siteSourcesDiscoveryService.DiscoverAsync(
            siteId,
            cancellationToken);

        EnsureDiscoverySucceeded(discovery.Status);

        foreach (var drive in discovery.Sources.Drives)
        {
            await driveAdministrationService.EnableDriveAsync(
                siteId,
                drive.DriveId,
                isIndexed: true,
                cancellationToken);
        }

        foreach (var list in discovery.Sources.Lists)
        {
            await listActivationService.SetIndexingAsync(
                siteId,
                list.ListId,
                isIndexed: true,
                cancellationToken);
        }

        return site;
    }

    private static void EnsureDiscoverySucceeded(
        Microsoft365SiteSourcesDiscoveryStatus status)
    {
        switch (status)
        {
            case Microsoft365SiteSourcesDiscoveryStatus.Succeeded:
                return;
            case Microsoft365SiteSourcesDiscoveryStatus.SiteNotFound:
                throw new NotFoundException("Microsoft 365 site was not found.");
            case Microsoft365SiteSourcesDiscoveryStatus.Forbidden:
                throw new ForbiddenException("The selected Microsoft 365 site cannot be accessed.");
            case Microsoft365SiteSourcesDiscoveryStatus.Throttled:
                throw new Microsoft365ExternalException("Microsoft 365 is temporarily unavailable.");
            default:
                throw new Microsoft365ExternalException("Microsoft 365 site contents could not be prepared.");
        }
    }
}
