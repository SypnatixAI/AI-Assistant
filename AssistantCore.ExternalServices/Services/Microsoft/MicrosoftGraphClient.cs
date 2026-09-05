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
}
