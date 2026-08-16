namespace AssistantCore.Service.Application.Models.Messages.Connectors.InternalData;

public sealed record InternalDataSearchRecord(
    InternalDataCategory Category,
    string Title,
    string Content,
    string Reference,
    DateTimeOffset OccurredAt,
    double? RelevanceScore = null);
