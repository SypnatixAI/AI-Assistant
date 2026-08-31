using System.Diagnostics;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public sealed class MessageToolOrchestrator(
    IAiModelTurnService modelTurnService,
    IOrchestrationContinuationPolicy continuationPolicy,
    IToolCallBatchExecutor toolCallBatchExecutor,
    IOrchestrationResultBuilder resultBuilder,
    IOptions<MessageOrchestrationOptions> options,
    TimeProvider timeProvider) : IMessageToolOrchestrator
{
    private static readonly ActivitySource RagActivitySource = new("AssistantCore.Rag");
    private readonly MessageOrchestrationOptions _options = options.Value;

    public async Task<MessageOrchestrationResult> OrchestrateAsync(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        IReadOnlyCollection<AiToolDefinition> availableTools,
        CancellationToken cancellationToken)
    {
        using var activity = StartActivity(selectedModel, streaming: false);
        var state = MessageOrchestrationState.Start(
            processing,
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
                    var result = resultBuilder.Build(state, modelResponse);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    return result;
                }

                if (continuation.ExceededBudget is { } exceededBudget)
                {
                    if (state.FinalResponseRequired)
                    {
                        throw new OrchestrationBudgetExceededException(exceededBudget);
                    }

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
                    var result = resultBuilder.Build(state, modelResponse);
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

    private OrchestrationExecutionLimits CreateExecutionLimits() =>
        new(
            TimeSpan.FromSeconds(_options.MaximumExecutionTimeSeconds),
            _options.MaximumToolCalls,
            _options.MaximumModelTokens,
            _options.MaximumEstimatedCost,
            _options.MaximumResultsPerTool,
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
