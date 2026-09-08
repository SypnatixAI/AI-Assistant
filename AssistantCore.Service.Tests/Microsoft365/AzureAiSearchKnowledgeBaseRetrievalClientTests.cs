using System.Net;
using System.Text.Json;
using AssistantCore.ExternalServices.Entities.Azure;
using AssistantCore.ExternalServices.Services.Azure;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class AzureAiSearchKnowledgeBaseRetrievalClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_AgenticRetrievalRequest_When_RetrieveAsync_Then_SendsExtractivePayloadWithLimitsAndFilter(
        string apiKey,
        string query,
        string filter)
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
                    response = new[]
                    {
                        new
                        {
                            role = "assistant",
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = "[{\"ref_id\":\"0\",\"chunkId\":\"chunk-atlas\",\"title\":\"Atlas\",\"content\":\"Atlas code project content\",\"siteId\":\"site-id\",\"driveId\":\"drive-id\",\"driveItemId\":\"drive-item-id\",\"url\":\"https://contoso.example/atlas\",\"modifiedAt\":\"2026-01-02T00:00:00Z\"}]"
                                }
                            }
                        }
                    },
                    references = new[]
                    {
                        new
                        {
                            id = "0",
                            rerankerScore = 3.2,
                            sourceData = new
                            {
                                chunkId = "chunk-atlas",
                                title = "Atlas",
                                content = "Atlas code project content",
                                siteId = "site-id",
                                driveId = "drive-id",
                                driveItemId = "drive-item-id",
                                url = "https://contoso.example/atlas",
                                modifiedAt = "2026-01-02T00:00:00Z"
                            }
                        }
                    },
                    activity = new[]
                    {
                        new
                        {
                            type = "searchIndex",
                            knowledgeSourceName = "synaptix-m365-knowledge-source",
                            count = 2,
                            elapsedMs = 125,
                            searchIndexArguments = new
                            {
                                search = "Atlas project code"
                            }
                        },
                        new
                        {
                            type = "searchIndex",
                            knowledgeSourceName = "synaptix-m365-knowledge-source",
                            count = 1,
                            elapsedMs = 110,
                            searchIndexArguments = new
                            {
                                search = "MecanoPlus financial risks"
                            }
                        }
                    }
                }))
            };
        }));
        var client = new AzureAiSearchKnowledgeBaseRetrievalClient(httpClient);

        // When
        var result = await client.RetrieveAsync(
            "https://search.example",
            apiKey,
            new AzureAiSearchKnowledgeBaseRetrievalRequest(
                "synaptix-m365-knowledge-base",
                "synaptix-m365-knowledge-source",
                query,
                [new AzureAiSearchKnowledgeBaseMessage("assistant", "Atlas context")],
                filter,
                10,
                30,
                6000),
            CancellationToken.None);

        // Then
        using var document = JsonDocument.Parse(payload!);
        Assert.Equal("extractiveData", document.RootElement.GetProperty("outputMode").GetString());
        Assert.True(document.RootElement.GetProperty("includeActivity").GetBoolean());
        Assert.Equal("low", document.RootElement.GetProperty("retrievalReasoningEffort").GetProperty("kind").GetString());
        Assert.Equal(30, document.RootElement.GetProperty("maxRuntimeInSeconds").GetInt32());
        Assert.Equal(6000, document.RootElement.GetProperty("maxOutputSize").GetInt32());
        Assert.Equal(50, document.RootElement.GetProperty("maxOutputDocuments").GetInt32());
        var sourceParams = document.RootElement.GetProperty("knowledgeSourceParams")[0];
        Assert.Equal("searchIndex", sourceParams.GetProperty("kind").GetString());
        Assert.Equal(50, sourceParams.GetProperty("maxOutputDocuments").GetInt32());
        Assert.True(sourceParams.GetProperty("alwaysQuerySource").GetBoolean());
        Assert.True(sourceParams.GetProperty("includeReferences").GetBoolean());
        Assert.True(sourceParams.GetProperty("includeReferenceSourceData").GetBoolean());
        Assert.Equal(filter, sourceParams.GetProperty("filterAddOn").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("messages").GetArrayLength());
        Assert.Single(result.References);
        Assert.Contains(result.Activity, activity => activity.Search == "Atlas project code");
        Assert.Contains(result.Activity, activity => activity.Search == "MecanoPlus financial risks");
    }
}
