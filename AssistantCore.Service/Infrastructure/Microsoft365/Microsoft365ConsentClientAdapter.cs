using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365ConsentClientAdapter(
    MicrosoftIdentityClient identityClient,
    MicrosoftGraphClient graphClient,
    IOptions<Microsoft365Options> options,
    TimeProvider timeProvider) : IMicrosoft365ConsentClient
{
    public Uri CreateAdminConsentUri(string state)
    {
        var configuration = options.Value;
        EnsureConfigured(configuration);
        return identityClient.CreateAdminConsentUri(
            configuration.AuthorityBaseUrl,
            configuration.ClientId,
            configuration.ConsentCallbackUrl,
            state);
    }

    public async Task<Microsoft365ConsentExchange> CompleteAdminConsentAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        EnsureConfigured(configuration);
        AssistantCore.ExternalServices.Entities.Microsoft.MicrosoftAuthorizationCodeToken token;
        AssistantCore.ExternalServices.Entities.Microsoft.MicrosoftTenant tenant;
        try
        {
            token = await identityClient.AcquireApplicationTokenAsync(
                configuration.AuthorityBaseUrl,
                tenantId,
                configuration.ClientId,
                configuration.ClientSecret,
                cancellationToken);
            tenant = await graphClient.GetCurrentTenantAsync(
                configuration.GraphBaseUrl,
                token.AccessToken,
                cancellationToken);
            if (!string.Equals(tenant.Id, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new MicrosoftExternalException(
                    "Microsoft tenant validation returned an unexpected tenant.");
            }
        }
        catch (MicrosoftExternalException exception)
        {
            throw new Microsoft365ExternalException("Microsoft 365 consent could not be completed.", exception);
        }

        return new Microsoft365ConsentExchange(
            tenant.Id,
            token.AccessToken,
            timeProvider.GetUtcNow().AddSeconds(token.ExpiresInSeconds));
    }

    private static void EnsureConfigured(Microsoft365Options configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.ClientId)
            || string.IsNullOrWhiteSpace(configuration.ClientSecret)
            || !Uri.TryCreate(configuration.ConsentCallbackUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Microsoft365 consent configuration is incomplete.");
        }
    }
}
