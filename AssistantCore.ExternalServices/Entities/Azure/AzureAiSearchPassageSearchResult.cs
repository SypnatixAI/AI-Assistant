namespace AssistantCore.ExternalServices.Entities.Azure;

public sealed record AzureAiSearchPassageSearchResult(
    string ChunkId,
    string Title,
    string Content,
    double? Score,
    string? Url = null,
    DateTimeOffset? ModifiedAt = null);
