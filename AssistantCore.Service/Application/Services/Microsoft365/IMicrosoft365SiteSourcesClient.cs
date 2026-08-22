using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365SiteSourcesClient
{
    Task<Microsoft365SiteSourcesDiscoveryResult> GetSiteSourcesAsync(
        string accessToken,
        string siteId,
        CancellationToken cancellationToken = default);
}
