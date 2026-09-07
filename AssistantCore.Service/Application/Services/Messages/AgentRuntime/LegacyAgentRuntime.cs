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
    public async Task<MessageOrchestrationResult> RunAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken)
    {
        var availableTools = await LoadAvailableToolsAsync(request, cancellationToken);

        return await orchestrator.OrchestrateAsync(
            request.Processing,
            request.ExecutionContext,
            request.SelectedModel,
            request.Processing.ConversationHistory,
            availableTools,
            cancellationToken);
    }

    public async Task<MessageOrchestrationResult> RunStreamingAsync(
        AgentTurnRequest request,
        AgentTurnStreamingCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callbacks);

        var availableTools = await LoadAvailableToolsAsync(request, cancellationToken);

        return await orchestrator.OrchestrateStreamingAsync(
            request.Processing,
            request.ExecutionContext,
            request.SelectedModel,
            request.Processing.ConversationHistory,
            availableTools,
            callbacks.OnProgress,
            callbacks.OnAnswerDelta,
            cancellationToken);
    }

    private Task<IReadOnlyCollection<AiToolDefinition>> LoadAvailableToolsAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken) =>
        toolRegistry.GetAvailableToolsAsync(
            request.Processing.OrganizationId,
            cancellationToken);
}
