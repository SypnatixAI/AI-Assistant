using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftIdentityClient(HttpClient httpClient)
{
    private const string GraphDefaultScope = "https://graph.microsoft.com/.default";

    public Uri CreateAuthorizationUri(
        string authorityBaseUrl,
        string clientId,
        string redirectUri,
        string state)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["response_mode"] = "query",
            ["scope"] = GraphDefaultScope,
            ["prompt"] = "admin_consent",
            ["state"] = state
        };

        var queryString = string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new Uri($"{authorityBaseUrl.TrimEnd('/')}/organizations/oauth2/v2.0/authorize?{queryString}");
    }

    public async Task<MicrosoftAuthorizationCodeToken> ExchangeAuthorizationCodeAsync(
        string authorityBaseUrl,
        string clientId,
        string clientSecret,
        string redirectUri,
        string code,
        CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["scope"] = GraphDefaultScope
        });
        using var response = await httpClient.PostAsync(
            $"{authorityBaseUrl.TrimEnd('/')}/organizations/oauth2/v2.0/token",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new MicrosoftExternalException(
                $"Microsoft authorization code exchange failed with status {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new MicrosoftExternalException("Microsoft token response was empty.");
        if (string.IsNullOrWhiteSpace(payload.AccessToken) || payload.ExpiresIn <= 0)
        {
            throw new MicrosoftExternalException("Microsoft token response was invalid.");
        }

        return new MicrosoftAuthorizationCodeToken(payload.AccessToken, payload.ExpiresIn);
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
