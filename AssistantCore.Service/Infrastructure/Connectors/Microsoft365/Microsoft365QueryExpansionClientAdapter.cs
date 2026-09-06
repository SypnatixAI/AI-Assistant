using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.OpenAI.Models;
using AssistantCore.ExternalServices.Services.OpenAI;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Infrastructure.AiModels.Configuration;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

public sealed class Microsoft365QueryExpansionClientAdapter(
    OpenAiResponsesClient client,
    IOptions<AiModelsOptions> aiModelsOptions) : IMicrosoft365QueryExpansionClient
{
    private const string SchemaName = "microsoft365_query_expansion";
    private const string SchemaDescription = "Alternative Microsoft 365 search queries.";
    private const string SchemaJson =
        """
        {
          "type": "object",
          "properties": {
            "queries": {
              "type": "array",
              "items": {
                "type": "string"
              }
            }
          },
          "required": ["queries"],
          "additionalProperties": false
        }
        """;

    private const string Instructions =
        """
        You generate alternative Microsoft 365 search queries for a RAG retrieval pipeline.
        Preserve the user's exact intent.
        Do not answer the question.
        Do not invent facts, policy values, dates, names, or constraints.
        Prefer concise professional vocabulary, synonyms, document titles, and business terms.
        Return only search formulations that could help retrieve relevant internal passages.
        """;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyCollection<string>> GenerateAsync(
        string query,
        int maximumGeneratedQueries,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (maximumGeneratedQueries <= 0)
        {
            return [];
        }

        string output;
        try
        {
            output = await client.CreateStructuredResponseAsync(
                new OpenAiStructuredResponseRequest(
                    aiModelsOptions.Value.DefaultModel,
                    Instructions,
                    CreateUserMessage(query, maximumGeneratedQueries),
                    SchemaName,
                    SchemaJson,
                    SchemaDescription),
                cancellationToken);
        }
        catch (OpenAiExternalException exception)
        {
            throw new InvalidOperationException(
                "The OpenAI query expansion request failed.",
                exception);
        }

        var response = JsonSerializer.Deserialize<QueryExpansionResponse>(
            output,
            SerializerOptions);

        return response?.Queries ?? throw new InvalidOperationException(
            "The query expansion response did not contain a queries array.");
    }

    private static string CreateUserMessage(string query, int maximumGeneratedQueries) =>
        $"""
        Original user query:
        {query}

        Generate at most {maximumGeneratedQueries} alternative search queries.
        """;

    private sealed record QueryExpansionResponse(
        [property: JsonPropertyName("queries")] IReadOnlyCollection<string> Queries);
}
