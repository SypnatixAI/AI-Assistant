using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365SiteClient
{
    Task<IReadOnlyCollection<Microsoft365AvailableSite>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<(string SiteId, string DisplayName, string WebUrl)> GetAsync(
        string tenantId,
        string siteId,
        CancellationToken cancellationToken = default);
}
