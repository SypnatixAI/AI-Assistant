namespace AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

public sealed record Microsoft365SearchRecord(
    string SourceType,
    string Title,
    string Content,
    string Reference,
    string? Url,
    DateTimeOffset? ModifiedAt,
    double? RelevanceScore);
