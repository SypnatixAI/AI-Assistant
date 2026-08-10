namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed record MessageOrchestrationResult(
    string Answer,
    string UsedModel,
    IReadOnlyCollection<RetrievedEvidence> UsedEvidence,
    IReadOnlyCollection<string> Warnings,
    AiModelUsage ModelUsage);
