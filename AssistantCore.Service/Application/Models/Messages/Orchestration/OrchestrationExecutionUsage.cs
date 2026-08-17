namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed record OrchestrationExecutionUsage(
    TimeSpan ExecutionTime,
    int InputTokens,
    int OutputTokens,
    int ModelCallCount,
    int ToolCallCount,
    decimal EstimatedCost,
    int ContextSize,
    int RepeatedToolCallCount)
{
    public int ModelTokenCount => InputTokens + OutputTokens;

    public static OrchestrationExecutionUsage Empty { get; } = new(
        ExecutionTime: TimeSpan.Zero,
        InputTokens: 0,
        OutputTokens: 0,
        ModelCallCount: 0,
        ToolCallCount: 0,
        EstimatedCost: 0m,
        ContextSize: 0,
        RepeatedToolCallCount: 0);
}
