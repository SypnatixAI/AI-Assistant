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
    bool IsFile)
{
    /// <summary>
    /// Drive that really owns the item. Null means the synchronized source
    /// drive is already canonical. Shared remoteItem results populate this so
    /// downstream work keeps ownerDriveId + driveItemId as the document key.
    /// </summary>
    public string? CanonicalDriveId { get; init; }
}
