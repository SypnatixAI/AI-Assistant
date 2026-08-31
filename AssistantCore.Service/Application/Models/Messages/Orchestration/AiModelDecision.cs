using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed record AiModelDecision(
    AiModelDecisionType Type,
    string Explanation,
    IReadOnlyCollection<AiRequestedToolCall> ToolCalls,
    string? Answer,
    IReadOnlyCollection<string> CitedEvidenceIds,
    string? ProgressMessage = null);
