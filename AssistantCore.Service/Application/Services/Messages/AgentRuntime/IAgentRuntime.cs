using AssistantCore.Service.Application.Models.Messages.AgentRuntime;

namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

public interface IAgentRuntime
{
    Task<AgentTurnResult> RunAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken);

    Task<AgentTurnResult> RunStreamingAsync(
        AgentTurnRequest request,
        AgentTurnStreamingCallbacks callbacks,
        CancellationToken cancellationToken);
}
