using System.Text.Json;
using AssistantCore.RagEvaluation.Models;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.AiModels;

namespace AssistantCore.RagEvaluation.Targets;

internal sealed class ScriptedAiModelProvider(
    RagEvaluationCase evaluationCase,
    IReadOnlyDictionary<string, RetrievedEvidence> evidenceByReference)
    : IAiModelProvider
{
    private int _modelCallCount;

    public string ProviderName => "OpenAI";

    public Task<AiModelResponse> GetNextActionAsync(
        AiModelRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _modelCallCount++;

        var completedRetrievalRounds = request.ToolResults.Count;
        return Task.FromResult(completedRetrievalRounds < evaluationCase.Fixture.RetrievalRounds.Count
            ? CreateToolResponse(completedRetrievalRounds)
            : CreateTerminalResponse());
    }

    public Task<AiModelResponse> GetNextActionStreamingAsync(
        AiModelRequest request,
        Func<string, CancellationToken, ValueTask> onTextDelta,
        CancellationToken cancellationToken) =>
        GetNextActionAsync(request, cancellationToken);

    private AiModelResponse CreateToolResponse(int retrievalRound)
    {
        var query = evaluationCase.Fixture.SearchQueries.ElementAt(retrievalRound);
        var toolCall = new AiRequestedToolCall(
            $"eval-call-{retrievalRound + 1}",
            AiToolNames.SearchInternalData,
            JsonSerializer.SerializeToElement(new { query }));

        return CreateResponse(new AiModelDecision(
            AiModelDecisionType.UseTools,
            "The evaluation fixture requires a retrieval round.",
            [toolCall],
            Answer: null,
            CitedEvidenceIds: []));
    }

    private AiModelResponse CreateTerminalResponse()
    {
        var citedEvidenceIds = evaluationCase.Fixture.CitedSourceReferences
            .Select(reference => evidenceByReference.TryGetValue(reference, out var evidence)
                ? evidence.EvidenceId
                : "evidence-000000000000000000000000")
            .ToArray();
        var decisionType = evaluationCase.Fixture.Outcome switch
        {
            EvaluationOutcome.Clarify => AiModelDecisionType.AskClarification,
            EvaluationOutcome.CannotAnswer => AiModelDecisionType.InsufficientInformation,
            _ => AiModelDecisionType.Answer
        };

        return CreateResponse(new AiModelDecision(
            decisionType,
            "The deterministic evaluation fixture produced a terminal decision.",
            ToolCalls: [],
            evaluationCase.Fixture.Answer,
            citedEvidenceIds));
    }

    private AiModelResponse CreateResponse(AiModelDecision decision) =>
        new(
            decision,
            new AiModelUsage(
                InputTokens: 10,
                OutputTokens: 5,
                ModelCallCount: 1,
                ToolCallCount: decision.ToolCalls.Count,
                EstimatedCost: 0m),
            new AiModelContinuationContext(
                ProviderName,
                $"eval-response-{_modelCallCount}"));
}
