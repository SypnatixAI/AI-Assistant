using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Repositories;

public sealed record Microsoft365ListItemWorkData(
    Guid OrganizationId,
    Microsoft365ListItemWorkType WorkType,
    string SiteId,
    string ListId,
    string ListItemId,
    string? ETag,
    DateTimeOffset? CreatedDateTime,
    DateTimeOffset? LastModifiedDateTime,
    string? WebUrl,
    string? FieldsJson,
    string DeduplicationKey,
    DateTimeOffset CreatedAt);
