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

    private sealed record OrganizationResponse(
        [property: JsonPropertyName("value")] IReadOnlyCollection<OrganizationItem> Value);

    private sealed record OrganizationItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("displayName")] string? DisplayName);
}
