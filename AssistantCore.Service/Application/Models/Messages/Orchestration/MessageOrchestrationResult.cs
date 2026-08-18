namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed record MessageOrchestrationResult(
    string Answer,
    string ModelName,
    IReadOnlyCollection<RetrievedEvidence> CitedEvidence,
    IReadOnlyCollection<string> Warnings,
    OrchestrationExecutionUsage Usage);
