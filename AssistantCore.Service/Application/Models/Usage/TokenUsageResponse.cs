namespace AssistantCore.Service.Application.Models.Usage;

public sealed record TokenUsageResponse(
    DateTimeOffset PeriodStartsAt,
    DateTimeOffset PeriodEndsAt,
    long TokenLimit,
    long TokensUsed,
    long TokensRemaining,
    bool IsExhausted);
