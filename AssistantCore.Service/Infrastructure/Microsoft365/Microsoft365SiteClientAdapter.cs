using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365SiteClientAdapter(
    MicrosoftIdentityClient identityClient,
    MicrosoftGraphSiteClient graphClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365SiteClient
{
    public async Task<(string SiteId, string DisplayName, string WebUrl)> GetAsync(
        string tenantId,
        string siteId,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        var token = await identityClient.AcquireApplicationTokenAsync(
            configuration.AuthorityBaseUrl,
            tenantId,
            configuration.ClientId,
            configuration.ClientSecret,
            cancellationToken);
        var site = await graphClient.GetAsync(
            configuration.GraphBaseUrl,
            token.AccessToken,
            siteId,
            cancellationToken);
        return (site.SiteId, site.DisplayName, site.WebUrl);
    }
}
