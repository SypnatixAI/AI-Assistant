using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Services.Usage;

public static class UsageConsumptionFactory
{
    public static TokenConsumption Create(
        Guid organizationId,
        Guid assistantMessageId,
        long inputTokens,
        long outputTokens,
        DateTimeOffset occurredAt)
    {
        var (periodStartsAt, periodEndsAt) = UsagePeriodCalculator.ComputeMonthlyPeriod(occurredAt);
        var totalTokens = checked(inputTokens + outputTokens);

        return new TokenConsumption
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            AssistantMessageId = assistantMessageId,
            PeriodStartsAt = periodStartsAt,
            PeriodEndsAt = periodEndsAt,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            CreatedAt = occurredAt
        };
    }
}
