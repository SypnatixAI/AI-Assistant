namespace AssistantCore.Repository.Domain.Entities;

public sealed class TokenConsumption
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid AssistantMessageId { get; set; }

    public DateTimeOffset PeriodStartsAt { get; set; }

    public DateTimeOffset PeriodEndsAt { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long TotalTokens { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Organization Organization { get; set; } = null!;

    public Message AssistantMessage { get; set; } = null!;
}
