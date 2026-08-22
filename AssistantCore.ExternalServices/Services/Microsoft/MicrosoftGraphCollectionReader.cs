using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssistantCore.ExternalServices.Services.Microsoft;

internal sealed class MicrosoftGraphCollectionReader(HttpClient httpClient)
{
    public async Task<IReadOnlyCollection<TResult>> ReadAsync<TItem, TResult>(
        Uri firstPageUri,
        string accessToken,
        Func<TItem, TResult> map,
        string resourceName,
        CancellationToken cancellationToken)
    {
        var results = new List<TResult>();
        var visitedPageUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Uri? pageUri = firstPageUri;

        while (pageUri is not null)
        {
            if (!visitedPageUris.Add(pageUri.AbsoluteUri))
            {
                throw new MicrosoftExternalException(
                    $"Microsoft Graph {resourceName} pagination contained a loop.");
            }

            var page = await ReadPageAsync<TItem>(
                pageUri,
                accessToken,
                resourceName,
                cancellationToken);

            results.AddRange(page.Value.Select(map));
            pageUri = ResolveNextPageUri(firstPageUri, page.NextLink, resourceName);
        }

        return results;
    }

    private async Task<GraphCollectionPage<TItem>> ReadPageAsync<TItem>(
        Uri pageUri,
        string accessToken,
        string resourceName,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, pageUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var retryAfter = response.Headers.RetryAfter;
            throw new MicrosoftExternalException(
                $"Microsoft Graph {resourceName} lookup failed with status {(int)response.StatusCode}.",
                statusCode: response.StatusCode,
                retryAfterDelay: retryAfter?.Delta,
                retryAfterAt: retryAfter?.Date);
        }

        GraphCollectionResponse<TItem>? page;
        try
        {
            page = await response.Content.ReadFromJsonAsync<GraphCollectionResponse<TItem>>(
                cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new MicrosoftExternalException(
                $"Microsoft Graph {resourceName} response was invalid.",
                exception);
        }

        if (page?.Value is null)
        {
            throw new MicrosoftExternalException(
                $"Microsoft Graph {resourceName} response was empty.");
        }

        return new GraphCollectionPage<TItem>(page.Value, page.NextLink);
    }

    private static Uri? ResolveNextPageUri(
        Uri firstPageUri,
        string? nextLink,
        string resourceName)
    {
        if (string.IsNullOrWhiteSpace(nextLink))
        {
            return null;
        }

        if (!Uri.TryCreate(nextLink, UriKind.Absolute, out var nextPageUri)
            || nextPageUri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(nextPageUri.Host, firstPageUri.Host, StringComparison.OrdinalIgnoreCase)
            || nextPageUri.Port != firstPageUri.Port)
        {
            throw new MicrosoftExternalException(
                $"Microsoft Graph {resourceName} pagination URL was not trusted.");
        }

        return nextPageUri;
    }

    private sealed record GraphCollectionResponse<TItem>(
        [property: JsonPropertyName("value")] IReadOnlyCollection<TItem>? Value,
        [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

    private sealed record GraphCollectionPage<TItem>(
        IReadOnlyCollection<TItem> Value,
        string? NextLink);
}
