namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed record OrchestrationExecutionLimits(
    TimeSpan MaximumExecutionTime,
    int MaximumToolCalls,
    int MaximumModelTokens,
    decimal MaximumEstimatedCost,
    int MaximumResultsPerTool,
    int MaximumContextSize,
    int MaximumRepeatedToolCalls,
    int MaximumParallelToolCalls = 4);
