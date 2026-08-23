using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class Microsoft365ListItemWork
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid Microsoft365SourceId { get; set; }

    public Guid Microsoft365SynchronizationId { get; set; }

    public Microsoft365ListItemWorkType WorkType { get; set; }

    public string SiteId { get; set; } = string.Empty;

    public string ListId { get; set; } = string.Empty;

    public string ListItemId { get; set; } = string.Empty;

    public string? ETag { get; set; }

    public DateTimeOffset? CreatedDateTime { get; set; }

    public DateTimeOffset? LastModifiedDateTime { get; set; }

    public string? WebUrl { get; set; }

    public string? FieldsJson { get; set; }

    public string DeduplicationKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public Organization Organization { get; set; } = null!;

    public Microsoft365Source Microsoft365Source { get; set; } = null!;

    public Microsoft365Synchronization Microsoft365Synchronization { get; set; } = null!;
}
