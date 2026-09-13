namespace AssistantCore.Service.Application.Models.Messages.AgentRuntime;

public sealed record AgentTurnUsage(
    TimeSpan ExecutionTime,
    int InputTokens,
    int OutputTokens,
    int ModelCallCount,
    int ToolCallCount);
