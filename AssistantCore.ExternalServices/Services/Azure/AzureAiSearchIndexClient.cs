using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssistantCore.ExternalServices.Entities.Azure;
using Azure.Core;
using Azure.Identity;

namespace AssistantCore.ExternalServices.Services.Azure;

public sealed class AzureAiSearchIndexClient
{
    private const string ApiVersion = "2025-09-01";
    private static readonly string[] SearchScopes = ["https://search.azure.com/.default"];
    private readonly HttpClient httpClient;
    private readonly TokenCredential credential;

    public AzureAiSearchIndexClient(HttpClient httpClient)
        : this(httpClient, new DefaultAzureCredential())
    {
    }

    internal AzureAiSearchIndexClient(HttpClient httpClient, TokenCredential credential)
    {
        this.httpClient = httpClient;
        this.credential = credential;
    }

    public async Task EnsureCreatedAsync(
        string endpoint,
        string indexName,
        string? apiKey,
        int embeddingDimensions,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            new Uri(endpoint),
            $"/indexes/{Uri.EscapeDataString(indexName)}?api-version={ApiVersion}");
        using var get = new HttpRequestMessage(HttpMethod.Get, uri);
        await AuthorizeAsync(get, apiKey, cancellationToken);
        using var existing = await httpClient.SendAsync(get, cancellationToken);
        if (!existing.IsSuccessStatusCode && existing.StatusCode != HttpStatusCode.NotFound)
        {
            throw new AzureAiSearchExternalException(
                $"Azure AI Search index validation failed with status {(int)existing.StatusCode}.");
        }

        var fields = AzureAiSearchMicrosoft365IndexDefinition.CreateFields()
            .Select(field => CreateField(field, embeddingDimensions))
            .ToArray();
        using var put = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(new
            {
                name = indexName,
                fields,
                vectorSearch = new
                {
                    algorithms = new[] { new { name = "m365-hnsw", kind = "hnsw" } },
                    profiles = new[] { new { name = "m365-vector-profile", algorithm = "m365-hnsw" } }
                },
                semantic = new
                {
                    configurations = new[]
                    {
                        new
                        {
                            name = "m365-semantic",
                            prioritizedFields = new
                            {
                                titleField = new { fieldName = "title" },
                                prioritizedContentFields = new[] { new { fieldName = "content" } },
                                prioritizedKeywordsFields = Array.Empty<object>()
                            }
                        }
                    }
                }
            })
        };
        await AuthorizeAsync(put, apiKey, cancellationToken);
        using var created = await httpClient.SendAsync(put, cancellationToken);
        if (!created.IsSuccessStatusCode)
        {
            throw new AzureAiSearchExternalException(
                $"Azure AI Search index creation failed with status {(int)created.StatusCode}.");
        }
    }

    private static object CreateField(AzureAiSearchIndexFieldDefinition field, int dimensions) =>
        field.Name == "contentVector"
            ? new
            {
                name = field.Name,
                type = field.Type,
                key = field.Key,
                searchable = true,
                filterable = field.Filterable,
                retrievable = false,
                dimensions = (int?)dimensions,
                vectorSearchProfile = (string?)"m365-vector-profile"
            }
            : new
            {
                name = field.Name,
                type = field.Type,
                key = field.Key,
                searchable = field.Searchable,
                filterable = field.Filterable,
                retrievable = field.Retrievable,
                dimensions = (int?)null,
                vectorSearchProfile = (string?)null
            };

    private async Task AuthorizeAsync(
        HttpRequestMessage request,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
            return;
        }

        var token = await credential.GetTokenAsync(new TokenRequestContext(SearchScopes), cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }
}
