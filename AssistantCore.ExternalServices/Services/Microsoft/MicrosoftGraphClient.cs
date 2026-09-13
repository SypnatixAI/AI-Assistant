using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftGraphClient(HttpClient httpClient)
{
    public async Task<MicrosoftTenant> GetCurrentTenantAsync(
        string graphBaseUrl,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{graphBaseUrl.TrimEnd('/')}/v1.0/organization?$select=id,displayName");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new MicrosoftExternalException(
                $"Microsoft tenant lookup failed with status {(int)response.StatusCode}.",
                statusCode: response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<OrganizationResponse>(cancellationToken)
            ?? throw new MicrosoftExternalException("Microsoft organization response was empty.");
        var organization = payload.Value.SingleOrDefault()
            ?? throw new MicrosoftExternalException("Microsoft organization response did not identify one tenant.");
        if (string.IsNullOrWhiteSpace(organization.Id))
        {
            throw new MicrosoftExternalException("Microsoft organization response contained an invalid tenant.");
        }

        return new MicrosoftTenant(organization.Id, organization.DisplayName);
    }

    public async Task<MicrosoftGraphUserDrive?> GetUserDriveAsync(
        string graphBaseUrl,
        string accessToken,
        string entraUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entraUserId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{graphBaseUrl.TrimEnd('/')}/v1.0/users/{Uri.EscapeDataString(entraUserId)}/drive?$select=id,name,webUrl");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new MicrosoftExternalException(
                $"Microsoft OneDrive lookup failed with status {(int)response.StatusCode}.",
                statusCode: response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<UserDriveResponse>(cancellationToken)
            ?? throw new MicrosoftExternalException("Microsoft OneDrive response was empty.");
        if (string.IsNullOrWhiteSpace(payload.Id))
        {
            throw new MicrosoftExternalException("Microsoft OneDrive response contained an invalid drive.");
        }

        return new MicrosoftGraphUserDrive(
            payload.Id,
            string.IsNullOrWhiteSpace(payload.Name) ? "OneDrive" : payload.Name,
            payload.WebUrl);
    }

    /// <summary>
    /// Appel Graph representatif des permissions applicatives requises par le
    /// connecteur (Sites.Read.All). Ne retourne aucune donnee : sert uniquement
    /// a verifier que le token applicatif peut reellement utiliser ces
    /// permissions, plutot que de se fier a la seule reussite du consentement.
    /// </summary>
    public async Task VerifyRequiredPermissionsAsync(
        string graphBaseUrl,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{graphBaseUrl.TrimEnd('/')}/v1.0/sites?search=*&$top=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new MicrosoftExternalException(
                $"Microsoft Graph permission verification failed with status {(int)response.StatusCode}.",
                statusCode: response.StatusCode);
        }
    }

    private sealed record OrganizationResponse(
        [property: JsonPropertyName("value")] IReadOnlyCollection<OrganizationItem> Value);

    private sealed record OrganizationItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("displayName")] string? DisplayName);

    private sealed record UserDriveResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("webUrl")] string? WebUrl);
}

public sealed record MicrosoftGraphUserDrive(
    string DriveId,
    string DisplayName,
    string? WebUrl);
