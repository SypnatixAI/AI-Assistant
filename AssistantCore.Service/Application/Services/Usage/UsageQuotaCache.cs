using System.Collections.Concurrent;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Usage;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Usage;

public sealed class UsageQuotaCache(IOptions<UsageOptions> options) : IUsageQuotaCache
{
    private readonly ConcurrentDictionary<Guid, UsageQuotaEntry> entries = new();
    private readonly long tokenLimit = options.Value.DefaultMonthlyTokenLimit;

    public void Initialize(
        DateTimeOffset periodStartsAt,
        DateTimeOffset periodEndsAt,
        IReadOnlyDictionary<Guid, long> tokensUsedByOrganization)
    {
        entries.Clear();

        foreach (var (organizationId, tokensUsed) in tokensUsedByOrganization)
        {
            entries[organizationId] = new UsageQuotaEntry(
                periodStartsAt,
                periodEndsAt,
                tokensUsed);
        }
    }

    public void EnsureQuotaAvailable(Guid organizationId, DateTimeOffset now)
    {
        var entry = GetCurrentEntry(organizationId, now);
        var snapshot = entry.Read();

        if (snapshot.TokensUsed >= tokenLimit)
        {
            throw new OrganizationTokenQuotaExceededException(snapshot.PeriodEndsAt);
        }
    }

    public MessageUsageResponse RecordConsumption(
        Guid organizationId,
        Guid assistantMessageId,
        long requestTokens,
        DateTimeOffset occurredAt)
    {
        var entry = GetCurrentEntry(organizationId, occurredAt);
        var snapshot = entry.AddOnce(assistantMessageId, requestTokens);
        var tokensRemaining = Math.Max(0, tokenLimit - snapshot.TokensUsed);

        return new MessageUsageResponse(
            requestTokens,
            tokenLimit,
            snapshot.TokensUsed,
            tokensRemaining,
            snapshot.PeriodEndsAt,
            tokensRemaining == 0);
    }

    private UsageQuotaEntry GetCurrentEntry(Guid organizationId, DateTimeOffset now)
    {
        while (true)
        {
            var entry = entries.GetOrAdd(
                organizationId,
                _ => CreateEmptyEntry(now));
            var snapshot = entry.Read();

            if (now >= snapshot.PeriodStartsAt && now < snapshot.PeriodEndsAt)
            {
                return entry;
            }

            var replacement = CreateEmptyEntry(now);
            if (entries.TryUpdate(organizationId, replacement, entry))
            {
                return replacement;
            }
        }
    }

    private static UsageQuotaEntry CreateEmptyEntry(DateTimeOffset now)
    {
        var (periodStartsAt, periodEndsAt) = UsagePeriodCalculator.ComputeMonthlyPeriod(now);
        return new UsageQuotaEntry(periodStartsAt, periodEndsAt, 0);
    }

    private sealed class UsageQuotaEntry(
        DateTimeOffset periodStartsAt,
        DateTimeOffset periodEndsAt,
        long tokensUsed)
    {
        private readonly object gate = new();
        private readonly HashSet<Guid> countedMessages = [];
        private long currentTokensUsed = tokensUsed;

        public UsageQuotaSnapshot Read()
        {
            lock (gate)
            {
                return CreateSnapshot();
            }
        }

        public UsageQuotaSnapshot AddOnce(Guid assistantMessageId, long requestTokens)
        {
            lock (gate)
            {
                if (countedMessages.Add(assistantMessageId))
                {
                    currentTokensUsed = checked(currentTokensUsed + requestTokens);
                }

                return CreateSnapshot();
            }
        }

        private UsageQuotaSnapshot CreateSnapshot() =>
            new(periodStartsAt, periodEndsAt, currentTokensUsed);
    }

    private sealed record UsageQuotaSnapshot(
        DateTimeOffset PeriodStartsAt,
        DateTimeOffset PeriodEndsAt,
        long TokensUsed);
}
