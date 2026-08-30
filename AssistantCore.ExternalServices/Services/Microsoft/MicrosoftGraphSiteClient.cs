using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftGraphSiteClient(HttpClient httpClient)
{
    public async Task<IReadOnlyCollection<MicrosoftSite>> ListAsync(
        string graphBaseUrl,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var graphBaseUri = CreateGraphBaseUri(graphBaseUrl);
        var sites = await ListSitesAsync(
            graphBaseUri,
            accessToken,
            "v1.0/sites?$select=id,displayName,webUrl",
            cancellationToken);

        if (sites.Count > 0)
        {
            return sites;
        }

        return await ListSitesAsync(
            graphBaseUri,
            accessToken,
            "v1.0/sites?search=*&$select=id,displayName,webUrl",
            cancellationToken);
    }

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

    private sealed record SiteCollectionResponse(
        [property: JsonPropertyName("value")] IReadOnlyCollection<SiteResponse> Value,
        [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

    private async Task<IReadOnlyCollection<MicrosoftSite>> ListSitesAsync(
        Uri graphBaseUri,
        string accessToken,
        string relativeRequestUri,
        CancellationToken cancellationToken)
    {
        Uri? nextPageUri = new(graphBaseUri, relativeRequestUri);
        var sites = new List<MicrosoftSite>();
        var visitedPageUris = new HashSet<string>(StringComparer.Ordinal);

        while (nextPageUri is not null)
        {
            EnsureTrustedGraphUri(graphBaseUri, nextPageUri);
            if (!visitedPageUris.Add(nextPageUri.AbsoluteUri))
            {
                throw new MicrosoftExternalException("Microsoft Graph site pagination contained a loop.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, nextPageUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new MicrosoftExternalException(
                    $"Microsoft Graph site listing failed with status {(int)response.StatusCode}.",
                    statusCode: response.StatusCode);
            }

            var page = await response.Content.ReadFromJsonAsync<SiteCollectionResponse>(cancellationToken)
                ?? throw new MicrosoftExternalException("Microsoft Graph returned an empty site collection response.");
            sites.AddRange(page.Value.Select(site =>
                new MicrosoftSite(site.Id, site.DisplayName, site.WebUrl)));
            nextPageUri = string.IsNullOrWhiteSpace(page.NextLink)
                ? null
                : new Uri(page.NextLink, UriKind.Absolute);
        }

        return sites;
    }

    private static Uri CreateGraphBaseUri(string graphBaseUrl)
    {
        if (!Uri.TryCreate(graphBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var graphBaseUri)
            || graphBaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Microsoft Graph base URL must use HTTPS.", nameof(graphBaseUrl));
        }

        return graphBaseUri;
    }

    private static void EnsureTrustedGraphUri(Uri graphBaseUri, Uri pageUri)
    {
        if (pageUri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(pageUri.Host, graphBaseUri.Host, StringComparison.OrdinalIgnoreCase)
            || pageUri.Port != graphBaseUri.Port)
        {
            throw new MicrosoftExternalException("Microsoft Graph returned an untrusted site pagination URL.");
        }
    }
}
