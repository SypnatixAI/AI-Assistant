using System.Diagnostics;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using Microsoft.Extensions.Options;
using AssistantCore.Service.Application.Services.Messages.Rag;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public sealed class MessageToolOrchestrator(
    IAiModelTurnService modelTurnService,
    IOrchestrationContinuationPolicy continuationPolicy,
    IToolCallBatchExecutor toolCallBatchExecutor,
    IOrchestrationResultBuilder resultBuilder,
    IOptions<MessageOrchestrationOptions> options,
    TimeProvider timeProvider,
    IAnswerGroundednessGuard? groundednessGuard = null) : IMessageToolOrchestrator
{
    private static readonly ActivitySource RagActivitySource = new("AssistantCore.Rag");
    private readonly MessageOrchestrationOptions _options = options.Value;

    public Task<MessageOrchestrationResult> OrchestrateAsync(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        IReadOnlyCollection<AiToolDefinition> availableTools,
        CancellationToken cancellationToken) =>
        OrchestrateAsync(
            processing,
            new ConnectorExecutionContext(
                processing.OrganizationId,
                processing.OwnerMemberId),
            selectedModel,
            conversationHistory,
            availableTools,
            cancellationToken);

    public async Task<MessageOrchestrationResult> OrchestrateAsync(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext,
        SelectedAiModel selectedModel,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        IReadOnlyCollection<AiToolDefinition> availableTools,
        CancellationToken cancellationToken)
    {
        using var activity = StartActivity(selectedModel, streaming: false);
        var state = MessageOrchestrationState.Start(
            processing,
            executionContext,
            selectedModel,
            conversationHistory,
            availableTools,
            CreateExecutionLimits(),
            timeProvider.GetUtcNow());

        while (true)
        {
            var modelResponse = await modelTurnService.RequestNextActionAsync(
                state,
                cancellationToken);
            var continuation = continuationPolicy.Evaluate(
                state,
                modelResponse.Decision);
            RecordTurn(activity, state, modelResponse.Decision, continuation);

            if (!continuation.CanContinue)
            {
                if (continuation.StopReason == OrchestrationStopReason.ModelCompleted)
                {
                    if (!TryBuildResultOrRequestCitationRepair(
                            state,
                            modelResponse,
                            out var result))
                    {
                        continue;
                    }

                    if (groundednessGuard is not null && modelResponse.Decision.Type == AiModelDecisionType.Answer)
                    {
                        result = await groundednessGuard.ValidateAsync(state, result, cancellationToken);
                        if (TryRequireGroundednessReformulation(state, result))
                        {
                            continue;
                        }
                    }
                    RagTelemetry.Record("rag.pipeline.duration_ms", (timeProvider.GetUtcNow() - state.Budget.StartedAtUtc).TotalMilliseconds);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    return result;
                }

                if (continuation.ExceededBudget is { } exceededBudget)
                {
                    if (state.FinalResponseRequired)
                    {
                        throw new OrchestrationBudgetExceededException(exceededBudget);
                    }

                    RecordBudgetExceededToolResults(state, modelResponse.Decision.ToolCalls);
                    state.RequireFinalResponse(exceededBudget);
                    continue;
                }

                throw new AiProviderInvalidResponseException(selectedModel.Provider);
            }

            await toolCallBatchExecutor.ExecuteAsync(
                state,
                modelResponse.Decision.ToolCalls,
                cancellationToken);
        }
    }

    public async Task<MessageOrchestrationResult> OrchestrateStreamingAsync(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext,
        SelectedAiModel selectedModel,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        IReadOnlyCollection<AiToolDefinition> availableTools,
        Func<string, CancellationToken, ValueTask> onProgress,
        Func<string, CancellationToken, ValueTask> onAnswerDelta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onProgress);
        ArgumentNullException.ThrowIfNull(onAnswerDelta);

        using var activity = StartActivity(selectedModel, streaming: true);
        var state = MessageOrchestrationState.Start(
            processing,
            executionContext,
            selectedModel,
            conversationHistory,
            availableTools,
            CreateExecutionLimits(),
            timeProvider.GetUtcNow());
        while (true)
        {
            var turnAnswerDeltas = new List<string>();
            var modelResponse = await modelTurnService.RequestNextActionStreamingAsync(
                state,
                (delta, _) =>
                {
                    turnAnswerDeltas.Add(delta);
                    return ValueTask.CompletedTask;
                },
                cancellationToken);
            var continuation = continuationPolicy.Evaluate(state, modelResponse.Decision);
            RecordTurn(activity, state, modelResponse.Decision, continuation);

            if (!continuation.CanContinue)
            {
                if (continuation.StopReason == OrchestrationStopReason.ModelCompleted)
                {
                    if (!TryBuildResultOrRequestCitationRepair(
                            state,
                            modelResponse,
                            out var result))
                    {
                        continue;
                    }

                    if (groundednessGuard is not null && modelResponse.Decision.Type == AiModelDecisionType.Answer)
                    {
                        result = await groundednessGuard.ValidateAsync(state, result, cancellationToken);
                        if (TryRequireGroundednessReformulation(state, result))
                        {
                            continue;
                        }
                    }
                    RagTelemetry.Record("rag.pipeline.duration_ms", (timeProvider.GetUtcNow() - state.Budget.StartedAtUtc).TotalMilliseconds);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    await WriteProgressAsync(modelResponse.Decision, onProgress, cancellationToken);
                    var streamedAnswer = string.Concat(turnAnswerDeltas);
                    if (turnAnswerDeltas.Count > 0
                        && string.Equals(streamedAnswer, result.Answer, StringComparison.Ordinal))
                    {
                        foreach (var delta in turnAnswerDeltas)
                        {
                            await onAnswerDelta(delta, cancellationToken);
                        }
                    }
                    else if (!string.IsNullOrEmpty(result.Answer))
                    {
                        await onAnswerDelta(result.Answer, cancellationToken);
                    }

                    return result;
                }

                if (continuation.ExceededBudget is { } exceededBudget)
                {
                    if (state.FinalResponseRequired)
                    {
                        throw new OrchestrationBudgetExceededException(exceededBudget);
                    }

                    RecordBudgetExceededToolResults(state, modelResponse.Decision.ToolCalls);
                    state.RequireFinalResponse(exceededBudget);
                    continue;
                }

                throw new AiProviderInvalidResponseException(selectedModel.Provider);
            }

            await WriteProgressAsync(modelResponse.Decision, onProgress, cancellationToken);
            await toolCallBatchExecutor.ExecuteAsync(
                state,
                modelResponse.Decision.ToolCalls,
                cancellationToken);
        }
    }

    private bool TryBuildResultOrRequestCitationRepair(
        MessageOrchestrationState state,
        AiModelResponse modelResponse,
        out MessageOrchestrationResult result)
    {
        try
        {
            result = resultBuilder.Build(state, modelResponse);
            return true;
        }
        catch (AiProviderInvalidCitationResponseException) when (
            !state.CitationRepairResponseRequired)
        {
            state.RequireCitationRepairResponse();
            result = null!;
            return false;
        }
    }

    private static async ValueTask WriteProgressAsync(
        AiModelDecision decision,
        Func<string, CancellationToken, ValueTask> onProgress,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(decision.ProgressMessage))
        {
            await onProgress(decision.ProgressMessage, cancellationToken);
        }
    }

    private static void RecordBudgetExceededToolResults(
        MessageOrchestrationState state,
        IReadOnlyCollection<AiRequestedToolCall> requestedToolCalls)
    {
        var skippedResults = requestedToolCalls
            .Select(toolCall => ToolExecutionResult.Failed(
                toolCall.CallId,
                "TOOL_BUDGET_EXCEEDED"))
            .ToArray();

        state.RecordToolResults(skippedResults);
    }

    private bool TryRequireGroundednessReformulation(
        MessageOrchestrationState state,
        MessageOrchestrationResult result)
    {
        if (groundednessGuard is null
            || !result.Warnings.Contains(
                IAnswerGroundednessGuard.ContentRejectedWarning,
                StringComparer.Ordinal)
            || state.GroundednessReformulationCount
                >= groundednessGuard.MaximumReformulationAttempts
            || timeProvider.GetUtcNow() >= state.Budget.DeadlineUtc)
        {
            return false;
        }

        return state.TryRequireGroundednessReformulation(
            groundednessGuard.MaximumReformulationAttempts);
    }

    private OrchestrationExecutionLimits CreateExecutionLimits() =>
        new(
            TimeSpan.FromSeconds(_options.MaximumExecutionTimeSeconds),
            _options.MaximumToolCalls,
            _options.MaximumModelTokens,
            _options.MaximumEstimatedCost,
            _options.RetrievalCandidateLimit,
            _options.FinalEvidenceLimit,
            _options.MaximumContextSize,
            _options.MaximumRepeatedToolCalls,
            _options.MaximumParallelToolCalls);

    private static Activity? StartActivity(SelectedAiModel selectedModel, bool streaming)
    {
        var activity = RagActivitySource.StartActivity("rag.orchestration", ActivityKind.Internal);
        activity?.SetTag("rag.model.provider", selectedModel.Provider);
        activity?.SetTag("rag.model.name", selectedModel.ModelName);
        activity?.SetTag("rag.streaming", streaming);
        return activity;
    }

    private static void RecordTurn(
        Activity? activity,
        MessageOrchestrationState state,
        AiModelDecision decision,
        OrchestrationContinuationDecision continuation)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("rag.model.calls", state.Budget.Usage.ModelCallCount);
        activity.SetTag("rag.tool.calls", state.Budget.Usage.ToolCallCount);
        activity.SetTag("rag.evidence.count", state.CollectedEvidence.Count);
        activity.SetTag("rag.decision", decision.Type.ToString());
        activity.SetTag("rag.stop_reason", continuation.StopReason?.ToString());
        activity.SetTag("rag.final_response_required", state.FinalResponseRequired);
    }
}
