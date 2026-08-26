namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365SiteClient
{
    Task<(string SiteId, string DisplayName, string WebUrl)> GetAsync(
        string tenantId,
        string siteId,
        CancellationToken cancellationToken = default);
}
