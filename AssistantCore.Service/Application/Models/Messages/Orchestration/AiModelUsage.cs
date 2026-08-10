namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed record AiModelUsage(
    int InputTokens,
    int OutputTokens,
    int ModelCallCount,
    int ToolCallCount,
    decimal? EstimatedCost);
