using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Usage;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Usage;

public sealed class UsageTrackingServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_AMessageProcessedMidMonth_When_RecordConsumptionAsync_Then_UpdatesTheInMemoryBalanceWithoutRepositoryIo(
        Guid organizationId,
        Guid assistantMessageId)
    {
        // Given
        var occurredAt = DateTimeOffset.Parse("2026-08-18T15:42:00Z");
        var repository = new StubTokenConsumptionRepository();
        var service = CreateService(
            repository,
            defaultMonthlyTokenLimit: 1_000_000,
            now: occurredAt);

        // When
        var response = await service.RecordConsumptionAsync(
            organizationId,
            assistantMessageId,
            100,
            20,
            occurredAt,
            CancellationToken.None);

        // Then
        Assert.Equal(120, response.RequestTokens);
        Assert.Equal(120, response.TokensUsed);
        Assert.Equal(999_880, response.TokensRemaining);
        Assert.Equal(DateTimeOffset.Parse("2026-09-01T00:00:00Z"), response.PeriodEndsAt);
        Assert.Equal(0, repository.TryRecordCallCount);
        Assert.Equal(0, repository.SumCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheSameAssistantMessage_When_RecordConsumptionAsync_Then_DoesNotDoubleCountInMemory(
        Guid organizationId,
        Guid assistantMessageId)
    {
        // Given
        var occurredAt = DateTimeOffset.Parse("2026-08-18T15:42:00Z");
        var repository = new StubTokenConsumptionRepository();
        var service = CreateService(
            repository,
            defaultMonthlyTokenLimit: 1_000_000,
            now: occurredAt);

        // When
        await service.RecordConsumptionAsync(
            organizationId,
            assistantMessageId,
            100,
            20,
            occurredAt,
            CancellationToken.None);
        var replayResponse = await service.RecordConsumptionAsync(
            organizationId,
            assistantMessageId,
            100,
            20,
            occurredAt,
            CancellationToken.None);

        // Then
        Assert.Equal(120, replayResponse.TokensUsed);
        Assert.Equal(999_880, replayResponse.TokensRemaining);
    }

    [Theory, AutoDomainData]
    public async Task Given_UsageBelowTheLimit_When_GetCurrentUsageAsync_Then_ReturnsTheRemainingBalance(
        Guid organizationId)
    {
        // Given
        var now = DateTimeOffset.Parse("2026-08-18T15:42:00Z");
        var repository = new StubTokenConsumptionRepository { SumToReturn = 428_000 };
        var service = CreateService(repository, defaultMonthlyTokenLimit: 1_000_000, now: now);

        // When
        var response = await service.GetCurrentUsageAsync(organizationId, CancellationToken.None);

        // Then
        Assert.Equal(1_000_000, response.TokenLimit);
        Assert.Equal(428_000, response.TokensUsed);
        Assert.Equal(572_000, response.TokensRemaining);
        Assert.False(response.IsExhausted);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnExhaustedCachedQuota_When_EnsureQuotaAvailableAsync_Then_ThrowsWithoutQueryingTheDatabase(
        Guid organizationId)
    {
        // Given
        var now = DateTimeOffset.Parse("2026-08-18T15:42:00Z");
        var repository = new StubTokenConsumptionRepository();
        var options = Options.Create(new UsageOptions { DefaultMonthlyTokenLimit = 1_000_000 });
        var cache = new UsageQuotaCache(options);
        cache.Initialize(
            DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            new Dictionary<Guid, long> { [organizationId] = 1_000_000 });
        var service = new UsageTrackingService(
            repository,
            cache,
            options,
            new FixedTimeProvider(now));

        // When
        var exception = await Assert.ThrowsAsync<OrganizationTokenQuotaExceededException>(() =>
            service.EnsureQuotaAvailableAsync(organizationId, CancellationToken.None));

        // Then
        Assert.Equal(DateTimeOffset.Parse("2026-09-01T00:00:00Z"), exception.PeriodEndsAt);
        Assert.Equal(0, repository.SumCallCount);
        Assert.Equal(0, repository.TryRecordCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_ARequestedOrganization_When_GetCurrentUsageAsync_Then_QueriesOnlyItsCurrentPeriod(
        Guid organizationId)
    {
        // Given
        var now = DateTimeOffset.Parse("2026-08-18T15:42:00Z");
        var repository = new StubTokenConsumptionRepository();
        var service = CreateService(repository, defaultMonthlyTokenLimit: 1_000_000, now: now);

        // When
        await service.GetCurrentUsageAsync(organizationId, CancellationToken.None);

        // Then
        Assert.Equal(organizationId, repository.ReceivedOrganizationId);
        Assert.Equal(DateTimeOffset.Parse("2026-08-01T00:00:00Z"), repository.ReceivedPeriodStartsAt);
        Assert.Equal(DateTimeOffset.Parse("2026-09-01T00:00:00Z"), repository.ReceivedPeriodEndsAt);
    }

    private static UsageTrackingService CreateService(
        ITokenConsumptionRepository repository,
        long defaultMonthlyTokenLimit,
        DateTimeOffset now)
    {
        var options = Options.Create(new UsageOptions
        {
            DefaultMonthlyTokenLimit = defaultMonthlyTokenLimit
        });

        return new UsageTrackingService(
            repository,
            new UsageQuotaCache(options),
            options,
            new FixedTimeProvider(now));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubTokenConsumptionRepository : ITokenConsumptionRepository
    {
        public long SumToReturn { get; init; }

        public int TryRecordCallCount { get; private set; }

        public int SumCallCount { get; private set; }

        public Guid? ReceivedOrganizationId { get; private set; }

        public DateTimeOffset? ReceivedPeriodStartsAt { get; private set; }

        public DateTimeOffset? ReceivedPeriodEndsAt { get; private set; }

        public Task<bool> TryRecordConsumptionAsync(
            TokenConsumption consumption,
            CancellationToken cancellationToken = default)
        {
            TryRecordCallCount++;
            return Task.FromResult(true);
        }

        public Task<long> SumTokensForPeriodAsync(
            Guid organizationId,
            DateTimeOffset periodStartsAt,
            DateTimeOffset periodEndsAt,
            CancellationToken cancellationToken = default)
        {
            SumCallCount++;
            ReceivedOrganizationId = organizationId;
            ReceivedPeriodStartsAt = periodStartsAt;
            ReceivedPeriodEndsAt = periodEndsAt;
            return Task.FromResult(SumToReturn);
        }

        public Task<IReadOnlyDictionary<Guid, long>> SumTokensByOrganizationForPeriodAsync(
            DateTimeOffset periodStartsAt,
            DateTimeOffset periodEndsAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, long>>(new Dictionary<Guid, long>());
    }
}
