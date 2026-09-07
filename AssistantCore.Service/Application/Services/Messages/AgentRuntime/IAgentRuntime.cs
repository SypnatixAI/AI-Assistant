using AssistantCore.Service.Application.Models.Messages.AgentRuntime;
using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

public interface IAgentRuntime
{
    Task<MessageOrchestrationResult> RunAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken);

    Task<MessageOrchestrationResult> RunStreamingAsync(
        AgentTurnRequest request,
        AgentTurnStreamingCallbacks callbacks,
        CancellationToken cancellationToken);
}
