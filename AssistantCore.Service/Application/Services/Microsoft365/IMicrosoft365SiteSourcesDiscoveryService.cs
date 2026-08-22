using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365SiteSourcesDiscoveryService
{
    Task<Microsoft365SiteSourcesDiscoveryResult> DiscoverAsync(
        string siteId,
        CancellationToken cancellationToken = default);
}
