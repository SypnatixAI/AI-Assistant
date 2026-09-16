using System.Net.Http.Headers;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365DelegatedTokenProvider(
    IHttpContextAccessor httpContextAccessor,
    MicrosoftIdentityClient identityClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365DelegatedTokenProvider
{
    private const string GraphScope = "https://graph.microsoft.com/.default";

    public async Task<string> GetGraphAccessTokenAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var header = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(header, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            throw new UnauthorizedAccessException(
                "A bearer token is required to access Microsoft 365 on behalf of the user.");
        }

        var configuration = options.Value;
        var token = await identityClient.AcquireOnBehalfOfTokenAsync(
            configuration.AuthorityBaseUrl,
            tenantId,
            configuration.ClientId,
            configuration.ClientSecret,
            authorization.Parameter,
            GraphScope,
            cancellationToken);

        return token.AccessToken;
    }
}
