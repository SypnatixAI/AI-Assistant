namespace AssistantCore.Service.Application.Models.Usage;

public sealed record MessageUsageResponse(
    long RequestTokens,
    long TokenLimit,
    long TokensUsed,
    long TokensRemaining,
    DateTimeOffset PeriodEndsAt,
    bool IsExhausted);
