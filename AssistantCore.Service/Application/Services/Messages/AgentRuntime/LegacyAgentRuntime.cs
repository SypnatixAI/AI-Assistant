using AssistantCore.Service.Application.Models.Messages.AgentRuntime;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

public sealed class LegacyAgentRuntime(
    IAiToolRegistry toolRegistry,
    IMessageToolOrchestrator orchestrator) : IAgentRuntime
{
    public async Task<AgentTurnResult> RunAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken)
    {
        var availableTools = await LoadAvailableToolsAsync(request, cancellationToken);

        var result = await orchestrator.OrchestrateAsync(
            request.Processing,
            request.ExecutionContext,
            request.SelectedModel,
            request.Processing.ConversationHistory,
            availableTools,
            cancellationToken);

        return CreateAgentTurnResult(result);
    }

    public async Task<AgentTurnResult> RunStreamingAsync(
        AgentTurnRequest request,
        AgentTurnStreamingCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callbacks);

        var availableTools = await LoadAvailableToolsAsync(request, cancellationToken);

        var result = await orchestrator.OrchestrateStreamingAsync(
            request.Processing,
            request.ExecutionContext,
            request.SelectedModel,
            request.Processing.ConversationHistory,
            availableTools,
            callbacks.OnProgress,
            callbacks.OnAnswerDelta,
            cancellationToken);

        return CreateAgentTurnResult(result);
    }

    private Task<IReadOnlyCollection<AiToolDefinition>> LoadAvailableToolsAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken) =>
        toolRegistry.GetAvailableToolsAsync(
            request.Processing.OrganizationId,
            cancellationToken);

    private static AgentTurnResult CreateAgentTurnResult(
        MessageOrchestrationResult result) =>
        new(
            result.Answer,
            result.ModelName,
            result.CitedEvidence,
            result.Warnings,
            new AgentTurnUsage(
                result.Usage.ExecutionTime,
                result.Usage.InputTokens,
                result.Usage.OutputTokens,
                result.Usage.ModelCallCount,
                result.Usage.ToolCallCount,
                result.Usage.EstimatedCost,
                result.Usage.ContextSize,
                result.Usage.RepeatedToolCallCount));
}
