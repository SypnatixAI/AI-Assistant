using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftGraphSiteClient(HttpClient httpClient)
{
    public async Task<MicrosoftSite> GetAsync(
        string graphBaseUrl,
        string accessToken,
        string siteId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{graphBaseUrl.TrimEnd('/')}/v1.0/sites/{Uri.EscapeDataString(siteId)}?$select=id,displayName,webUrl");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new MicrosoftExternalException(
                $"Microsoft Graph site lookup failed with status {(int)response.StatusCode}.",
                statusCode: response.StatusCode);
        }

        var site = await response.Content.ReadFromJsonAsync<SiteResponse>(cancellationToken)
            ?? throw new MicrosoftExternalException("Microsoft Graph returned an empty site response.");
        return new MicrosoftSite(site.Id, site.DisplayName, site.WebUrl);
    }

    private sealed record SiteResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("webUrl")] string WebUrl);
}
