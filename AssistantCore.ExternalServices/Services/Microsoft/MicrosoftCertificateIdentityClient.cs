using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftCertificateIdentityClient
{
    public async Task<MicrosoftAuthorizationCodeToken> AcquireApplicationTokenForScopeAsync(
        string authorityBaseUrl,
        string tenantId,
        string clientId,
        string certificatePath,
        string certificatePassword,
        string scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePassword);

        try
        {
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath,
                certificatePassword);
            return await AcquireApplicationTokenForScopeAsync(
                authorityBaseUrl,
                tenantId,
                clientId,
                certificate,
                scope,
                cancellationToken);
        }
        catch (Exception exception) when (exception is
            CryptographicException or
            IOException or
            UnauthorizedAccessException)
        {
            throw new MicrosoftExternalException(
                "Microsoft certificate application token acquisition failed.",
                exception);
        }
    }

    public async Task<MicrosoftAuthorizationCodeToken> AcquireApplicationTokenFromBase64ForScopeAsync(
        string authorityBaseUrl,
        string tenantId,
        string clientId,
        string certificateBase64,
        string certificatePassword,
        string scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateBase64);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePassword);

        try
        {
            var certificateBytes = Convert.FromBase64String(certificateBase64);
            using var certificate = X509CertificateLoader.LoadPkcs12(
                certificateBytes,
                certificatePassword);
            return await AcquireApplicationTokenForScopeAsync(
                authorityBaseUrl,
                tenantId,
                clientId,
                certificate,
                scope,
                cancellationToken);
        }
        catch (Exception exception) when (exception is
            FormatException or
            CryptographicException)
        {
            throw new MicrosoftExternalException(
                "Microsoft certificate application token acquisition failed.",
                exception);
        }
    }

    private static async Task<MicrosoftAuthorizationCodeToken> AcquireApplicationTokenForScopeAsync(
        string authorityBaseUrl,
        string tenantId,
        string clientId,
        X509Certificate2 certificate,
        string scope,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(authorityBaseUrl, UriKind.Absolute, out var authorityUri)
            || authorityUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Microsoft authority URL must use HTTPS.", nameof(authorityBaseUrl));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (!Guid.TryParse(clientId, out var parsedClientId) || parsedClientId == Guid.Empty)
        {
            throw new ArgumentException("A valid Microsoft client identifier is required.", nameof(clientId));
        }

        if (!Uri.TryCreate(scope, UriKind.Absolute, out var scopeUri)
            || scopeUri.Scheme != Uri.UriSchemeHttps
            || !scope.EndsWith("/.default", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Microsoft application token scope must be an HTTPS .default scope.",
                nameof(scope));
        }

        try
        {
            var credential = new ClientCertificateCredential(
                tenantId,
                parsedClientId.ToString("D"),
                certificate,
                new ClientCertificateCredentialOptions
                {
                    AuthorityHost = new Uri($"{authorityUri.GetLeftPart(UriPartial.Authority)}/")
                });
            var accessToken = await credential.GetTokenAsync(
                new TokenRequestContext([scope]),
                cancellationToken);
            var expiresInSeconds = Math.Clamp(
                (long)(accessToken.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds,
                1,
                int.MaxValue);

            return new MicrosoftAuthorizationCodeToken(
                accessToken.Token,
                (int)expiresInSeconds);
        }
        catch (Exception exception) when (exception is
            AuthenticationFailedException)
        {
            throw new MicrosoftExternalException(
                "Microsoft certificate application token acquisition failed.",
                exception);
        }
    }
}
