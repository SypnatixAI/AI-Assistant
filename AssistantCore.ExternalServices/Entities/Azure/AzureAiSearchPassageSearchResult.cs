namespace AssistantCore.ExternalServices.Entities.Azure;

public sealed record AzureAiSearchPassageSearchResult(
    string ChunkId,
    string Title,
    string Content,
    double? Score,
    double? SemanticScore,
    string? SiteId = null,
    string? DriveId = null,
    string? DriveItemId = null,
    string? Url = null,
    DateTimeOffset? ModifiedAt = null);
