using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class Message
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public MessageRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public MessageProcessingStatus ProcessingStatus { get; set; }

    public string? Model { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public ICollection<MessageSource> Sources { get; set; } = [];
}
