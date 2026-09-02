namespace AssistantCore.ExternalServices.Entities.Azure;

public sealed record AzureAiSearchPassageSearchResult(
    string ChunkId,
    string Title,
    string Content,
    double? Score,
    double? SemanticScore,
    string? Url = null,
    DateTimeOffset? ModifiedAt = null);
