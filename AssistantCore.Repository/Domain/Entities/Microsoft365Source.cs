using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class Microsoft365Source
{
    public Guid Id { get; set; }

    public Guid Microsoft365ConnectionId { get; set; }

    public Microsoft365SourceKind Kind { get; set; }

    public string ExternalResourceId { get; set; } = string.Empty;

    public string? ParentExternalResourceId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? WebUrl { get; set; }

    public Microsoft365SourceStatus Status { get; set; }

    public bool IsIndexed { get; set; }

    public string? DeltaLink { get; set; }

    public DateTimeOffset DiscoveredAt { get; set; }

    public DateTimeOffset? EnabledAt { get; set; }

    public DateTimeOffset? LastSuccessfulSynchronizationAt { get; set; }

    public DateTimeOffset? NextSynchronizationAt { get; set; }

    public string? LastErrorCode { get; set; }

    public Microsoft365Connection Microsoft365Connection { get; set; } = null!;

    public ICollection<Microsoft365Subscription> Subscriptions { get; set; } = [];

    public ICollection<Microsoft365Synchronization> Synchronizations { get; set; } = [];
}
