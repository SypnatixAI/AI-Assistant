using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Repositories;

public sealed record Microsoft365DocumentWorkData(
    Guid OrganizationId,
    Microsoft365DocumentWorkType WorkType,
    string SiteId,
    string DriveId,
    string DriveItemId,
    string? Name,
    string? ETag,
    DateTimeOffset? CreatedDateTime,
    DateTimeOffset? LastModifiedDateTime,
    string? WebUrl,
    long? Size,
    string? MimeType,
    string DeduplicationKey,
    DateTimeOffset CreatedAt);
