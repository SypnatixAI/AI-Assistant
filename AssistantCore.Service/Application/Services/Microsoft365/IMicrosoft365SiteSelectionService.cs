using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365SiteSelectionService
{
    Task<Microsoft365SiteResponse> SelectAsync(
        string siteId,
        CancellationToken cancellationToken = default);
}
