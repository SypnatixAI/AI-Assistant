using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Usage;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Usage;

public sealed class UsageQuotaCacheTests
{
    [Theory, AutoDomainData]
    public void Given_AnExhaustedWarmCache_When_EnsuringQuotaAvailability_Then_ThrowsFromMemory(
        Guid organizationId)
    {
        // Given
        var cache = CreateCache(1_000);
        var periodStartsAt = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var periodEndsAt = DateTimeOffset.Parse("2026-09-01T00:00:00Z");
        cache.Initialize(
            periodStartsAt,
            periodEndsAt,
            new Dictionary<Guid, long> { [organizationId] = 1_000 });

        // When
        var exception = Assert.Throws<OrganizationTokenQuotaExceededException>(() =>
            cache.EnsureQuotaAvailable(
                organizationId,
                DateTimeOffset.Parse("2026-08-18T15:42:00Z")));

        // Then
        Assert.Equal(periodEndsAt, exception.PeriodEndsAt);
    }

    [Theory, AutoDomainData]
    public void Given_ARequestCrossesTheLimit_When_TheNextRequestChecksQuota_Then_ItIsRejected(
        Guid organizationId,
        Guid assistantMessageId)
    {
        // Given
        var cache = CreateCache(1_000);
        var occurredAt = DateTimeOffset.Parse("2026-08-18T15:42:00Z");
        cache.Initialize(
            DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            new Dictionary<Guid, long> { [organizationId] = 950 });

        // When
        var usage = cache.RecordConsumption(
            organizationId,
            assistantMessageId,
            100,
            occurredAt);

        // Then
        Assert.Equal(1_050, usage.TokensUsed);
        Assert.Equal(0, usage.TokensRemaining);
        Assert.True(usage.IsExhausted);
        Assert.Throws<OrganizationTokenQuotaExceededException>(() =>
            cache.EnsureQuotaAvailable(organizationId, occurredAt));
    }

    [Theory, AutoDomainData]
    public void Given_TheSameAssistantMessageIsRecordedTwice_When_UpdatingUsage_Then_ItIsCountedOnce(
        Guid organizationId,
        Guid assistantMessageId)
    {
        // Given
        var cache = CreateCache(1_000);
        var occurredAt = DateTimeOffset.Parse("2026-08-18T15:42:00Z");

        // When
        cache.RecordConsumption(organizationId, assistantMessageId, 125, occurredAt);
        var replay = cache.RecordConsumption(organizationId, assistantMessageId, 125, occurredAt);

        // Then
        Assert.Equal(125, replay.TokensUsed);
        Assert.Equal(875, replay.TokensRemaining);
    }

    [Theory, AutoDomainData]
    public void Given_TheCachedPeriodHasEnded_When_ARequestChecksQuota_Then_AFreshPeriodStarts(
        Guid organizationId)
    {
        // Given
        var cache = CreateCache(1_000);
        cache.Initialize(
            DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            new Dictionary<Guid, long> { [organizationId] = 1_000 });

        // When
        var exception = Record.Exception(() =>
            cache.EnsureQuotaAvailable(
                organizationId,
                DateTimeOffset.Parse("2026-09-01T00:00:01Z")));

        // Then
        Assert.Null(exception);
    }

    private static UsageQuotaCache CreateCache(long limit) =>
        new(Options.Create(new UsageOptions { DefaultMonthlyTokenLimit = limit }));
}
