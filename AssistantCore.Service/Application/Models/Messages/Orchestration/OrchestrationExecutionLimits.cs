namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed record OrchestrationExecutionLimits(
    TimeSpan MaximumExecutionTime,
    int MaximumToolCalls,
    int MaximumModelTokens,
    decimal MaximumEstimatedCost,
    int RetrievalCandidateLimit,
    int FinalEvidenceLimit,
    int MaximumContextSize,
    int MaximumRepeatedToolCalls,
    int MaximumParallelToolCalls = 4);
