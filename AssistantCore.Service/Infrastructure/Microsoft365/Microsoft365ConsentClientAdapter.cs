using System.Net;
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
    private const int MaximumTenantValidationAttempts = 3;

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
        try
        {
            for (var attempt = 1; attempt <= MaximumTenantValidationAttempts; attempt++)
            {
                var token = await identityClient.AcquireApplicationTokenAsync(
                    configuration.AuthorityBaseUrl,
                    tenantId,
                    configuration.ClientId,
                    configuration.ClientSecret,
                    cancellationToken);

                try
                {
                    var tenant = await graphClient.GetCurrentTenantAsync(
                        configuration.GraphBaseUrl,
                        token.AccessToken,
                        cancellationToken);
                    if (!string.Equals(tenant.Id, tenantId, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new MicrosoftExternalException(
                            "Microsoft tenant validation returned an unexpected tenant.");
                    }

                    return new Microsoft365ConsentExchange(
                        tenant.Id,
                        token.AccessToken,
                        timeProvider.GetUtcNow().AddSeconds(token.ExpiresInSeconds));
                }
                catch (MicrosoftExternalException exception)
                    when (exception.StatusCode == HttpStatusCode.Forbidden
                        && attempt < MaximumTenantValidationAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                }
            }
        }
        catch (MicrosoftExternalException exception)
        {
            throw new Microsoft365ExternalException("Microsoft 365 consent could not be completed.", exception);
        }

        throw new InvalidOperationException("Microsoft tenant validation did not complete.");
    }

    public async Task<bool> VerifyRequiredPermissionsAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        EnsureConfigured(configuration);

        try
        {
            await graphClient.VerifyRequiredPermissionsAsync(
                configuration.GraphBaseUrl,
                accessToken,
                cancellationToken);
            return true;
        }
        catch (MicrosoftExternalException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return false;
        }
        catch (MicrosoftExternalException exception)
        {
            throw new Microsoft365ExternalException(
                "Microsoft 365 permission verification could not be completed.",
                exception);
        }
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
