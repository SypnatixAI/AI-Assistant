using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class Microsoft365Subscription
{
    public Guid Id { get; set; }

    public Guid Microsoft365SourceId { get; set; }

    public Guid OrganizationId { get; set; }

    public string Resource { get; set; } = string.Empty;

    public string? MicrosoftSubscriptionId { get; set; }

    public string? ProtectedClientState { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? LastRenewedAt { get; set; }

    public Microsoft365SubscriptionStatus Status { get; set; }

    public string? LastErrorCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Organization Organization { get; set; } = null!;

    public Microsoft365Source Microsoft365Source { get; set; } = null!;
}
