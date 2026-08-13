namespace AssistantCore.Repository.Domain.Entities;

public sealed class MessageSource
{
    public Guid Id { get; set; }

    public Guid MessageId { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Reference { get; set; } = string.Empty;

    public string? Url { get; set; }

    public DateTimeOffset? SourceDate { get; set; }

    public Message Message { get; set; } = null!;
}
