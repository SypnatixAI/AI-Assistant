using AssistantCore.Service.Application.Models.Messages.AgentRuntime;
using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Commands.SendMessage;

internal static class AgentTurnResultAdapter
{
    public static MessageOrchestrationResult ToMessageOrchestrationResult(
        AgentTurnResult result) =>
        new(
            result.Content,
            result.ModelName,
            result.Citations,
            result.Warnings,
            new OrchestrationExecutionUsage(
                result.Usage.ExecutionTime,
                result.Usage.InputTokens,
                result.Usage.OutputTokens,
                result.Usage.ModelCallCount,
                result.Usage.ToolCallCount,
                result.Usage.EstimatedCost,
                result.Usage.ContextSize,
                result.Usage.RepeatedToolCallCount));
}
