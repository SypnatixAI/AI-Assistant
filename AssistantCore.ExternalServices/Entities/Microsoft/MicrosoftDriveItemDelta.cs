namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftDriveItemDelta(
    string Id,
    string? Name,
    string? ETag,
    DateTimeOffset? CreatedDateTime,
    DateTimeOffset? LastModifiedDateTime,
    string? WebUrl,
    long? Size,
    string? MimeType,
    bool IsDeleted,
    bool IsFolder,
    bool IsFile);
