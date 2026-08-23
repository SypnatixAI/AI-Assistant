namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365DriveItemDelta(
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
