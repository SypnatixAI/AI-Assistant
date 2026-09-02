using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Azure;
using Azure.Core;
using Azure.Identity;

namespace AssistantCore.ExternalServices.Services.Azure;

public sealed class AzureAiSearchPassageSearchClient
{
    private const string ApiVersion = "2025-09-01";
    private const int SemanticCandidateCount = 50;
    private static readonly string[] SearchScopes = ["https://search.azure.com/.default"];
    private readonly HttpClient httpClient;
    private readonly TokenCredential credential;

    public AzureAiSearchPassageSearchClient(HttpClient httpClient)
        : this(httpClient, new DefaultAzureCredential())
    {
    }

    internal AzureAiSearchPassageSearchClient(HttpClient httpClient, TokenCredential credential)
    {
        this.httpClient = httpClient;
        this.credential = credential;
    }

    public async Task<IReadOnlyCollection<AzureAiSearchPassageSearchResult>> SearchAsync(
        string endpoint,
        string indexName,
        string? apiKey,
        string query,
        string filter,
        int maximumResults,
        CancellationToken cancellationToken = default)
    {
        return await SearchAsync(
            endpoint,
            indexName,
            apiKey,
            query,
            filter,
            maximumResults,
            null,
            true,
            "m365-semantic",
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<AzureAiSearchPassageSearchResult>> SearchAsync(
        string endpoint,
        string indexName,
        string? apiKey,
        string query,
        string filter,
        int maximumResults,
        IReadOnlyList<float>? queryVector,
        bool semanticRankingEnabled,
        string semanticConfigurationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(filter);
        if (maximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }
        if (semanticRankingEnabled)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(semanticConfigurationName);
        }

        var payload = new Dictionary<string, object?>
        {
            ["search"] = query,
            ["filter"] = filter,
            ["select"] = "chunkId,title,content,url,modifiedAt",
            ["top"] = maximumResults
        };
        if (semanticRankingEnabled)
        {
            payload["queryType"] = "semantic";
            payload["semanticConfiguration"] = semanticConfigurationName;
        }
        if (queryVector is { Count: > 0 })
        {
            payload["vectorQueries"] = new[]
            {
                new
                {
                    kind = "vector",
                    vector = queryVector,
                    fields = "contentVector",
                    k = Math.Max(maximumResults, SemanticCandidateCount)
                }
            };
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            CreateRequestUri(endpoint, indexName))
        {
            Content = JsonContent.Create(payload)
        };
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(SearchScopes),
                cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        else
        {
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AzureAiSearchExternalException(
                $"Azure AI Search rejected a passage search with status {(int)response.StatusCode}.");
        }

        SearchResponse? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<SearchResponse>(
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw new AzureAiSearchExternalException(
                "Azure AI Search returned an invalid passage search response.");
        }

        if (result?.Value is null)
        {
            throw new AzureAiSearchExternalException(
                "Azure AI Search returned an empty passage search response.");
        }

        return result.Value.Select(MapResult).ToArray();
    }

    private static AzureAiSearchPassageSearchResult MapResult(SearchDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.ChunkId)
            || string.IsNullOrWhiteSpace(document.Title)
            || string.IsNullOrWhiteSpace(document.Content))
        {
            throw new AzureAiSearchExternalException(
                "Azure AI Search returned an incomplete passage.");
        }

        return new AzureAiSearchPassageSearchResult(
            document.ChunkId,
            document.Title,
            document.Content,
            document.RerankerScore ?? document.Score,
            document.Url,
            document.ModifiedAt);
    }

    private static Uri CreateRequestUri(string endpoint, string indexName)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            || endpointUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("An HTTPS Azure AI Search endpoint is required.", nameof(endpoint));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        return new Uri(
            endpointUri,
            $"/indexes('{Uri.EscapeDataString(indexName)}')/docs/search.post.search?api-version={ApiVersion}");
    }

    private sealed record SearchResponse(
        [property: JsonPropertyName("value")] IReadOnlyCollection<SearchDocument> Value);

    private sealed record SearchDocument(
        [property: JsonPropertyName("chunkId")] string? ChunkId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("modifiedAt")] DateTimeOffset? ModifiedAt,
        [property: JsonPropertyName("@search.score")] double? Score,
        [property: JsonPropertyName("@search.rerankerScore")] double? RerankerScore);
}
