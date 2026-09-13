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
    bool IsFile)
{
    /// <summary>
    /// Drive that really owns the item. Null for regular delta items where the
    /// source drive is already the canonical drive. Shared search results set
    /// this from remoteItem.parentReference.driveId.
    /// </summary>
    public string? CanonicalDriveId { get; init; }
}
