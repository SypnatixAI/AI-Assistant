namespace AssistantCore.Service.Application.Models.Messages.Evidence;

public sealed record EvidenceCandidate(
    string? SourceType,
    string? Title,
    string? Content,
    string? Reference,
    string? Url,
    DateTimeOffset? OccurredAt,
    double? RelevanceScore);
