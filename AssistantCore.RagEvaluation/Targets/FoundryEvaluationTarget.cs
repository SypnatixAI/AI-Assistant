using System.Diagnostics;
using System.Text.Json;
using AssistantCore.RagEvaluation.Models;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.AgentRuntime;

namespace AssistantCore.RagEvaluation.Targets;

internal sealed class FoundryEvaluationTarget(IFoundryAgentClient client) : IRagEvaluationTarget
{
    private static readonly JsonElement EnterpriseSearchSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "query": { "type": "string" },
            "sourceTypes": { "type": "array", "items": { "type": "string" } },
            "dateFrom": { "type": "string" },
            "dateTo": { "type": "string" }
          },
          "required": ["query"],
          "additionalProperties": false
        }
        """).RootElement.Clone();

    public async Task<EvaluationObservation> RunAsync(
        RagEvaluationCase evaluationCase,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var retrievedReferences = new List<string>();
        var searchQueries = new List<string>();
        var retrievalRound = 0;

        try
        {
            Task<string> ExecuteToolAsync(FoundryAgentToolCall call, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (!string.Equals(call.Name, "EnterpriseSearch", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Unexpected Foundry tool '{call.Name}'.");
                }

                if (call.Arguments.TryGetProperty("query", out var query))
                {
                    searchQueries.Add(query.GetString() ?? string.Empty);
                }

                var references = evaluationCase.Fixture.RetrievalRounds
                    .ElementAtOrDefault(retrievalRound++) ?? [];
                var documents = references
                    .Select(reference => evaluationCase.Documents.Single(document =>
                        string.Equals(document.Reference, reference, StringComparison.Ordinal)))
                    .Where(document => document.Allowed)
                    .ToArray();
                retrievedReferences.AddRange(documents.Select(document => document.Reference));

                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    status = "Succeeded",
                    evidence = documents.Select(document => new
                    {
                        evidenceId = document.Reference,
                        title = document.Title,
                        content = document.Content,
                        reference = document.Reference
                    }),
                    warnings = Array.Empty<string>()
                }));
            }

            var conversation = evaluationCase.Conversation.ToArray();
            var result = await client.RunAsync(
                new FoundryAgentClientRequest(
                    CreateHistory(conversation),
                    conversation.Last(),
                    evaluationCase.ToolsAvailable
                        ? [new FoundryAgentToolDefinition(
                            "EnterpriseSearch",
                            "Search authorized internal enterprise information.",
                            EnterpriseSearchSchema)]
                        : []),
                ExecuteToolAsync,
                cancellationToken);
            stopwatch.Stop();

            return new EvaluationObservation(
                evaluationCase.Id,
                InferOutcome(result.Content),
                result.Content,
                retrievedReferences,
                retrievedReferences.Distinct(StringComparer.Ordinal).ToArray(),
                searchQueries,
                result.ModelCallCount,
                retrievalRound,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            return new EvaluationObservation(
                evaluationCase.Id,
                EvaluationOutcome.Error,
                string.Empty,
                retrievedReferences,
                [],
                searchQueries,
                0,
                retrievalRound,
                stopwatch.ElapsedMilliseconds,
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static IReadOnlyCollection<AiConversationMessage> CreateHistory(string[] conversation) =>
        conversation
            .Take(Math.Max(0, conversation.Length - 1))
            .Select((content, index) => new AiConversationMessage(
                index % 2 == 0 ? AiConversationRole.User : AiConversationRole.Assistant,
                content))
            .ToArray();

    private static EvaluationOutcome InferOutcome(string answer)
    {
        var normalized = answer.ToLowerInvariant();
        if (normalized.Contains("quel dossier", StringComparison.Ordinal)
            || normalized.Contains("which file", StringComparison.Ordinal)
            || normalized.Contains("could you clarify", StringComparison.Ordinal))
        {
            return EvaluationOutcome.Clarify;
        }

        if (normalized.Contains("ne peux pas confirmer", StringComparison.Ordinal)
            || normalized.Contains("aucune information interne", StringComparison.Ordinal)
            || normalized.Contains("cannot confirm", StringComparison.Ordinal))
        {
            return EvaluationOutcome.CannotAnswer;
        }

        return EvaluationOutcome.Answer;
    }
}
