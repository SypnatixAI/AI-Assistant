using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365ApplicationTokenClientAdapter(
    MicrosoftIdentityClient identityClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365ApplicationTokenClient
{
    public async Task<string> AcquireGraphTokenAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        var token = await identityClient.AcquireApplicationTokenAsync(
            configuration.AuthorityBaseUrl,
            tenantId,
            configuration.ClientId,
            configuration.ClientSecret,
            cancellationToken);
        return token.AccessToken;
    }
}
