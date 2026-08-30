using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class ConversationPurgeRequest
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid OrganizationId { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset PurgeAfter { get; set; }

    public ConversationPurgeStatus Status { get; set; }

    public Conversation Conversation { get; set; } = null!;
}
