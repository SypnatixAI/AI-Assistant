using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class Microsoft365DocumentWork
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid Microsoft365SourceId { get; set; }

    public Guid Microsoft365SynchronizationId { get; set; }

    public Microsoft365DocumentWorkType WorkType { get; set; }

    public string SiteId { get; set; } = string.Empty;

    public string DriveId { get; set; } = string.Empty;

    public string DriveItemId { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? ETag { get; set; }

    public DateTimeOffset? CreatedDateTime { get; set; }

    public DateTimeOffset? LastModifiedDateTime { get; set; }

    public string? WebUrl { get; set; }

    public long? Size { get; set; }

    public string? MimeType { get; set; }

    public string DeduplicationKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public Organization Organization { get; set; } = null!;

    public Microsoft365Source Microsoft365Source { get; set; } = null!;

    public Microsoft365Synchronization Microsoft365Synchronization { get; set; } = null!;
}
