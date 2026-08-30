using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365SiteDiscoveryService
{
    Task<IReadOnlyCollection<Microsoft365AvailableSiteResponse>> GetAvailableSitesAsync(
        CancellationToken cancellationToken = default);
}
