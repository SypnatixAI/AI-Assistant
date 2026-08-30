using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
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

            if (!continuation.CanContinue)
            {
                if (continuation.StopReason == OrchestrationStopReason.ModelCompleted)
                {
                    return resultBuilder.Build(state, modelResponse);
                }

                if (continuation.ExceededBudget is { } exceededBudget)
                {
                    throw new OrchestrationBudgetExceededException(exceededBudget);
                }

                throw new AiProviderInvalidResponseException(selectedModel.Provider);
            }

            await toolCallBatchExecutor.ExecuteAsync(
                state,
                modelResponse.Decision.ToolCalls,
                cancellationToken);
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
}
