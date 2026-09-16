using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftIdentityClient(HttpClient httpClient)
{
    private const string GraphDefaultScope = "https://graph.microsoft.com/.default";
    private const string OnBehalfOfGrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";

    public Uri CreateAdminConsentUri(
        string authorityBaseUrl,
        string clientId,
        string redirectUri,
        string state)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = GraphDefaultScope,
            ["state"] = state
        };

        var queryString = string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new Uri($"{authorityBaseUrl.TrimEnd('/')}/organizations/v2.0/adminconsent?{queryString}");
    }

    public Task<MicrosoftAuthorizationCodeToken> AcquireApplicationTokenAsync(
        string authorityBaseUrl,
        string tenantId,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default) =>
        AcquireApplicationTokenForScopeAsync(
            authorityBaseUrl,
            tenantId,
            clientId,
            clientSecret,
            GraphDefaultScope,
            cancellationToken);

    public async Task<MicrosoftAuthorizationCodeToken> AcquireApplicationTokenForScopeAsync(
        string authorityBaseUrl,
        string tenantId,
        string clientId,
        string clientSecret,
        string scope,
        CancellationToken cancellationToken = default)
    {
        ValidateApplicationScope(scope);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "client_credentials",
            ["scope"] = scope
        });
        return await AcquireTokenAsync(
            authorityBaseUrl,
            tenantId,
            content,
            "Microsoft application token acquisition",
            cancellationToken);
    }

    public async Task<MicrosoftAuthorizationCodeToken> AcquireOnBehalfOfTokenAsync(
        string authorityBaseUrl,
        string tenantId,
        string clientId,
        string clientSecret,
        string userAccessToken,
        string scope,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userAccessToken))
        {
            throw new ArgumentException("The user access token is required.", nameof(userAccessToken));
        }
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("The delegated scope is required.", nameof(scope));
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = OnBehalfOfGrantType,
            ["requested_token_use"] = "on_behalf_of",
            ["assertion"] = userAccessToken,
            ["scope"] = scope
        });
        return await AcquireTokenAsync(
            authorityBaseUrl,
            tenantId,
            content,
            "Microsoft on-behalf-of token acquisition",
            cancellationToken);
    }

    private async Task<MicrosoftAuthorizationCodeToken> AcquireTokenAsync(
        string authorityBaseUrl,
        string tenantId,
        HttpContent content,
        string operationName,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(
            $"{authorityBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/token",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new MicrosoftExternalException(
                $"{operationName} failed with status {(int)response.StatusCode}.",
                statusCode: response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new MicrosoftExternalException("Microsoft token response was empty.");
        if (string.IsNullOrWhiteSpace(payload.AccessToken) || payload.ExpiresIn <= 0)
        {
            throw new MicrosoftExternalException("Microsoft token response was invalid.");
        }

        return new MicrosoftAuthorizationCodeToken(payload.AccessToken, payload.ExpiresIn);
    }

    private static void ValidateApplicationScope(string scope)
    {
        if (!Uri.TryCreate(scope, UriKind.Absolute, out var scopeUri)
            || scopeUri.Scheme != Uri.UriSchemeHttps
            || !scope.EndsWith("/.default", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Microsoft application token scope must be an HTTPS .default scope.",
                nameof(scope));
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
