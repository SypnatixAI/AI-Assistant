namespace AssistantCore.Repository.Domain.Entities;

public sealed class MessageWarning
{
    public Guid Id { get; set; }

    public Guid MessageId { get; set; }

    public string Content { get; set; } = string.Empty;

    public Message Message { get; set; } = null!;
}
