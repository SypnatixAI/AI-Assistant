namespace AssistantCore.Service.Application.Configuration;

public sealed class MessageOrchestrationOptions
{
    public const string SectionName = "Messages:Orchestration";

    public int MaximumExecutionTimeSeconds { get; init; }

    public int MaximumToolCalls { get; init; }

    public int MaximumModelTokens { get; init; }

    public decimal MaximumEstimatedCost { get; init; }

    public int MaximumResultsPerTool { get; init; }

    public int MaximumContextSize { get; init; }

    public int MaximumRepeatedToolCalls { get; init; }

    public int MaximumParallelToolCalls { get; init; }
}
