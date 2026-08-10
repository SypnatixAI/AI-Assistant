namespace AssistantCore.Service.Application.Models.Messages;

public sealed record RetrievedEvidence(
    string EvidenceId,
    string SourceType,
    string Title,
    string Content,
    string Reference,
    string? Url,
    DateTimeOffset? OccurredAt);
