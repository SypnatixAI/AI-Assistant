using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed record AiModelNextAction(
    AiModelNextActionType Action,
    string Reason,
    IReadOnlyCollection<AiRequestedToolCall> RequestedToolCalls,
    string? ProposedAnswer,
    IReadOnlyCollection<string> CitedEvidenceIds);
