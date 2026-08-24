namespace AssistantCore.Repository.Domain.Entities;

public sealed class Microsoft365IndexedContent
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid Microsoft365SourceId { get; set; }
    public string ExternalContentId { get; set; } = string.Empty;
    public string? SiteUrl { get; set; }
    public string? AclFingerprint { get; set; }
    public bool IsAvailable { get; set; }
    public DateTimeOffset? NextAclReconciliationAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Organization Organization { get; set; } = null!;
    public Microsoft365Source Microsoft365Source { get; set; } = null!;
    public ICollection<Microsoft365IndexedPassage> Passages { get; set; } = [];
}
