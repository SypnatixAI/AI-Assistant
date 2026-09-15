using System.Net.Http.Headers;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Messages.AgentRuntime;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Foundry;

public sealed class FoundryDelegatedAccessTokenProvider(
    IHttpContextAccessor httpContextAccessor,
    ICurrentIdentity currentIdentity,
    MicrosoftIdentityClient identityClient,
    IOptions<Microsoft365Options> microsoft365Options) : IFoundryDelegatedAccessTokenProvider
{
    private const string FoundryScope = "https://ai.azure.com/.default";

    public async Task<FoundryDelegatedAccessToken> GetAsync(
        CancellationToken cancellationToken)
    {
        var identity = currentIdentity.GetIdentity();
        if (identity.Provider != IdentityProvider.MicrosoftEntraId
            || string.IsNullOrWhiteSpace(identity.ExternalOrganizationId))
        {
            throw new UnauthorizedAccessException(
                "Work IQ requires an authenticated Microsoft Entra user.");
        }

        var assertion = GetBearerToken();
        var options = microsoft365Options.Value;
        var token = await identityClient.AcquireOnBehalfOfTokenAsync(
            options.AuthorityBaseUrl,
            identity.ExternalOrganizationId,
            options.ClientId,
            options.ClientSecret,
            assertion,
            FoundryScope,
            cancellationToken);

        return new FoundryDelegatedAccessToken(
            token.AccessToken,
            DateTimeOffset.UtcNow.AddSeconds(token.ExpiresInSeconds));
    }

    private string GetBearerToken()
    {
        var header = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(header, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            throw new UnauthorizedAccessException(
                "A bearer token is required to call the Foundry agent on behalf of the user.");
        }

        return authorization.Parameter;
    }
}
