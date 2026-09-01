using System.Diagnostics;
using System.Text.Json;
using AssistantCore.RagEvaluation.Models;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Evidence;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using Microsoft.Extensions.Options;

namespace AssistantCore.RagEvaluation.Targets;

internal sealed class OrchestrationEvaluationTarget(
    Func<RagEvaluationCase, IReadOnlyDictionary<string, RetrievedEvidence>, IAiModelProvider>
        providerFactory,
    TimeProvider timeProvider) : IRagEvaluationTarget
{
    private static readonly AiToolDefinition InternalSearchTool = new(
        AiToolNames.SearchInternalData,
        "Search the organization's authorized internal information.",
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { query = new { type = "string" } },
            required = new[] { "query" },
            additionalProperties = false
        }));

    public async Task<EvaluationObservation> RunAsync(
        RagEvaluationCase evaluationCase,
        string model,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var evidenceByReference = NormalizeDocuments(evaluationCase.Documents);
        var toolExecutor = new FixtureToolCallBatchExecutor(
            evaluationCase,
            evidenceByReference,
            timeProvider);
        var recordingTurnService = new RecordingAiModelTurnService(
            new AiModelTurnService(
                [providerFactory(evaluationCase, evidenceByReference)],
                timeProvider));
        var orchestrator = new MessageToolOrchestrator(
            recordingTurnService,
            new OrchestrationContinuationPolicy(new ToolCallFingerprintGenerator(), timeProvider),
            toolExecutor,
            new OrchestrationResultBuilder(new EvidenceCitationResolver()),
            Options.Create(CreateOptions()),
            timeProvider);

        try
        {
            var result = await orchestrator.OrchestrateAsync(
                CreateProcessing(evaluationCase),
                new SelectedAiModel("OpenAI", model),
                CreateHistory(evaluationCase.Conversation),
                evaluationCase.ToolsAvailable ? [InternalSearchTool] : [],
                cancellationToken);
            var terminalDecision = recordingTurnService.Decisions.Last();

            return new EvaluationObservation(
                evaluationCase.Id,
                MapOutcome(terminalDecision.Type),
                result.Answer,
                toolExecutor.RetrievedReferences,
                result.CitedEvidence.Select(evidence => evidence.Reference).ToArray(),
                toolExecutor.SearchQueries,
                recordingTurnService.Decisions.Count,
                toolExecutor.ToolCallCount,
                stopwatch.ElapsedMilliseconds);
        }
        catch (AiProviderInvalidResponseException exception)
        {
            return CreateFailureObservation(
                evaluationCase.Id,
                EvaluationOutcome.Rejected,
                recordingTurnService,
                toolExecutor,
                stopwatch,
                exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CreateFailureObservation(
                evaluationCase.Id,
                EvaluationOutcome.Error,
                recordingTurnService,
                toolExecutor,
                stopwatch,
                exception);
        }
    }

    private static IReadOnlyDictionary<string, RetrievedEvidence> NormalizeDocuments(
        IReadOnlyCollection<EvaluationDocument> documents)
    {
        if (documents.Count == 0)
        {
            return new Dictionary<string, RetrievedEvidence>(StringComparer.Ordinal);
        }

        var candidates = documents.Select(document => new EvidenceCandidate(
            "evaluation-fixture",
            document.Title,
            document.Content,
            document.Reference,
            Url: null,
            OccurredAt: null,
            RelevanceScore: 1d)).ToArray();
        return new EvidenceNormalizer()
            .Normalize(candidates, new EvidenceNormalizationOptions(20_000, documents.Count))
            .ToDictionary(evidence => evidence.Reference, StringComparer.Ordinal);
    }

    private static StartedMessageProcessing CreateProcessing(RagEvaluationCase evaluationCase) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            evaluationCase.Conversation.Last());

    private static IReadOnlyCollection<AiConversationMessage> CreateHistory(
        IReadOnlyCollection<string> conversation) =>
        conversation
            .Take(Math.Max(0, conversation.Count - 1))
            .Select((content, index) => new AiConversationMessage(
                index % 2 == 0 ? AiConversationRole.Assistant : AiConversationRole.User,
                content))
            .ToArray();

    private static MessageOrchestrationOptions CreateOptions() => new()
    {
        MaximumExecutionTimeSeconds = 60,
        MaximumToolCalls = 8,
        MaximumModelTokens = 20_000,
        MaximumEstimatedCost = 5m,
        RetrievalCandidateLimit = 20,
        FinalEvidenceLimit = 10,
        MaximumContextSize = 100_000,
        MaximumRepeatedToolCalls = 2,
        MaximumParallelToolCalls = 4
    };

    private static EvaluationOutcome MapOutcome(AiModelDecisionType decisionType) =>
        decisionType switch
        {
            AiModelDecisionType.Answer => EvaluationOutcome.Answer,
            AiModelDecisionType.AskClarification => EvaluationOutcome.Clarify,
            AiModelDecisionType.InsufficientInformation => EvaluationOutcome.CannotAnswer,
            _ => EvaluationOutcome.Error
        };

    private static EvaluationObservation CreateFailureObservation(
        string caseId,
        EvaluationOutcome outcome,
        RecordingAiModelTurnService turnService,
        FixtureToolCallBatchExecutor toolExecutor,
        Stopwatch stopwatch,
        Exception exception) =>
        new(
            caseId,
            outcome,
            string.Empty,
            toolExecutor.RetrievedReferences,
            [],
            toolExecutor.SearchQueries,
            turnService.Decisions.Count,
            toolExecutor.ToolCallCount,
            stopwatch.ElapsedMilliseconds,
            $"{exception.GetType().Name}: {exception.Message}");
}
