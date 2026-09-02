using System.Net;
using System.Text.Json;
using AssistantCore.ExternalServices.Services.Azure;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using AssistantCore.Service.Infrastructure.Connectors.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SearchRepositoryTests
{
    [Theory, AutoDomainData]
    public async Task Given_AuthenticatedSecurityContext_When_SearchAsync_Then_ImposesOrganizationUserAndGroups(
        Guid organizationId,
        Guid userId,
        Guid firstGroupId,
        Guid secondGroupId,
        string apiKey,
        string query,
        string chunkId,
        string title,
        string content,
        float[] queryVector)
    {
        // Given
        string? payload = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            payload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    value = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["@search.score"] = 1.0,
                            ["@search.rerankerScore"] = 2.0,
                            ["chunkId"] = chunkId,
                            ["title"] = title,
                            ["content"] = content
                        }
                    }
                }))
            };
        }));
        var repository = new Microsoft365SearchRepositoryAdapter(
            new AzureAiSearchPassageSearchClient(httpClient),
            Options.Create(new AzureAiSearchOptions
            {
                Endpoint = "https://search.example",
                IndexName = "content-index",
                ApiKey = apiKey
            }),
            new StubEmbeddingGenerator(queryVector));
        var dateFrom = new DateOnly(2026, 1, 2);
        var dateTo = new DateOnly(2026, 2, 3);
        var parameters = new Microsoft365SearchParameters(
            query,
            ["sharepoint"],
            dateFrom,
            dateTo,
            new Microsoft365SearchSecurityContext(
                organizationId,
                userId.ToString("D"),
                [firstGroupId.ToString("D"), secondGroupId.ToString("D")]),
            10);

        // When
        var results = await repository.SearchAsync(parameters, CancellationToken.None);

        // Then
        Assert.Single(results);
        using var document = JsonDocument.Parse(payload!);
        var filter = document.RootElement.GetProperty("filter").GetString();
        Assert.Contains($"organizationId eq '{organizationId:D}'", filter, StringComparison.Ordinal);
        Assert.Contains("isAvailable eq true", filter, StringComparison.Ordinal);
        Assert.Contains($"allowedUserIds/any(id: id eq '{userId:D}')", filter, StringComparison.Ordinal);
        Assert.Contains(firstGroupId.ToString("D"), filter, StringComparison.Ordinal);
        Assert.Contains(secondGroupId.ToString("D"), filter, StringComparison.Ordinal);
        Assert.Contains("sourceType eq 'sharepoint'", filter, StringComparison.Ordinal);
        Assert.Contains("modifiedAt ge 2026-01-02T00:00:00Z", filter, StringComparison.Ordinal);
        Assert.Contains("modifiedAt lt 2026-02-04T00:00:00Z", filter, StringComparison.Ordinal);
        Assert.Equal(
            "chunkId,title,content,url,modifiedAt",
            document.RootElement.GetProperty("select").GetString());
        Assert.DoesNotContain("allowedUserIds", document.RootElement.GetProperty("select").GetString());
        var vectorQuery = document.RootElement.GetProperty("vectorQueries")[0];
        Assert.Equal("contentVector", vectorQuery.GetProperty("fields").GetString());
        Assert.Equal(50, vectorQuery.GetProperty("k").GetInt32());
        Assert.Equal(queryVector, vectorQuery.GetProperty("vector").EnumerateArray().Select(value => value.GetSingle()));
        Assert.Equal("semantic", document.RootElement.GetProperty("queryType").GetString());
        Assert.Equal("m365-semantic", document.RootElement.GetProperty("semanticConfiguration").GetString());
    }

    [Theory, AutoDomainData]
    public async Task Given_SemanticRankingDisabled_When_SearchAsync_Then_UsesHybridSearchWithoutSemanticParameters(
        Guid organizationId,
        Guid userId,
        string apiKey,
        string query,
        string chunkId,
        string title,
        string content,
        float[] queryVector)
    {
        // Given
        string? payload = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            payload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    value = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["@search.score"] = 1.0,
                            ["chunkId"] = chunkId,
                            ["title"] = title,
                            ["content"] = content
                        }
                    }
                }))
            };
        }));
        var repository = new Microsoft365SearchRepositoryAdapter(
            new AzureAiSearchPassageSearchClient(httpClient),
            Options.Create(new AzureAiSearchOptions
            {
                Endpoint = "https://search.example",
                IndexName = "content-index",
                ApiKey = apiKey,
                SemanticRankingEnabled = false
            }),
            new StubEmbeddingGenerator(queryVector));
        var parameters = new Microsoft365SearchParameters(
            query,
            ["sharepoint"],
            null,
            null,
            new Microsoft365SearchSecurityContext(
                organizationId,
                userId.ToString("D"),
                []),
            10);

        // When
        var results = await repository.SearchAsync(parameters, CancellationToken.None);

        // Then
        Assert.Single(results);
        using var document = JsonDocument.Parse(payload!);
        Assert.False(document.RootElement.TryGetProperty("queryType", out _));
        Assert.False(document.RootElement.TryGetProperty("semanticConfiguration", out _));
        Assert.True(document.RootElement.TryGetProperty("vectorQueries", out _));
    }

    private sealed class StubEmbeddingGenerator(IReadOnlyList<float> vector)
        : IMicrosoft365EmbeddingGenerator
    {
        public Task<IReadOnlyList<IReadOnlyList<float>>> CreateAsync(
            IReadOnlyCollection<string> contents,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>([vector]);
    }
}
