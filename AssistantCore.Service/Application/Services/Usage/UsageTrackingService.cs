using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Usage;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Usage;

public sealed class UsageTrackingService(
    ITokenConsumptionRepository consumptionRepository,
    IUsageQuotaCache quotaCache,
    UsageConsumptionQueue consumptionQueue,
    IOptions<UsageOptions> options,
    TimeProvider timeProvider) : IUsageTrackingService
{
    public Task EnsureQuotaAvailableAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        quotaCache.EnsureQuotaAvailable(organizationId, timeProvider.GetUtcNow());
        return Task.CompletedTask;
    }

    public Task<MessageUsageResponse> RecordConsumptionAsync(
        Guid organizationId,
        Guid assistantMessageId,
        long inputTokens,
        long outputTokens,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (periodStartsAt, periodEndsAt) = UsagePeriodCalculator.ComputeMonthlyPeriod(occurredAt);
        var requestTokens = checked(inputTokens + outputTokens);
        var consumption = new TokenConsumption
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            AssistantMessageId = assistantMessageId,
            PeriodStartsAt = periodStartsAt,
            PeriodEndsAt = periodEndsAt,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = requestTokens,
            CreatedAt = occurredAt
        };

        consumptionQueue.Enqueue(consumption);
        var usage = quotaCache.RecordConsumption(
            organizationId,
            requestTokens,
            occurredAt);

        return Task.FromResult(usage);
    }

    public async Task<TokenUsageResponse> GetCurrentUsageAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var (periodStartsAt, periodEndsAt) = UsagePeriodCalculator.ComputeMonthlyPeriod(
            timeProvider.GetUtcNow());
        var tokensUsed = await consumptionRepository.SumTokensForPeriodAsync(
            organizationId,
            periodStartsAt,
            periodEndsAt,
            cancellationToken);
        var tokenLimit = options.Value.DefaultMonthlyTokenLimit;
        var tokensRemaining = Math.Max(0, tokenLimit - tokensUsed);

        return new TokenUsageResponse(
            periodStartsAt,
            periodEndsAt,
            tokenLimit,
            tokensUsed,
            tokensRemaining,
            tokensRemaining == 0);
    }
}
