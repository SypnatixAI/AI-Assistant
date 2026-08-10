namespace AssistantCore.Service.Application.Models.Messages.Tools;

public sealed record ToolExecutionResult(
    string ToolCallId,
    ToolExecutionStatus Status,
    IReadOnlyCollection<RetrievedEvidence> Evidence,
    IReadOnlyCollection<string> Warnings,
    string? ErrorCode);
