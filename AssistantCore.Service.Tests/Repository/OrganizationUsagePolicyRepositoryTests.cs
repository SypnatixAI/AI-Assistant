using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class OrganizationUsagePolicyRepositoryTests
{
    private const string CorrelationId = "request-8f812";

    [Fact]
    public async Task Given_NoExistingPolicy_When_CreateAsync_Then_CreatesTheFirstVersionAndStagesAnAuditEntry()
    {
        // Given
        await using var dbContext = CreateDbContext();
        var organization = await SeedOrganizationAsync(dbContext);
        var actorId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-08-18T15:00:00Z");
        var effectiveAt = DateTimeOffset.Parse("2026-09-01T00:00:00Z");
        var administrativeAuditRepository = new RecordingAdministrativeAuditRepository();
        var repository = new OrganizationUsagePolicyRepository(dbContext, administrativeAuditRepository);

        // When
        var result = await repository.CreateAsync(
            organization.Id,
            1_000_000,
            UsagePolicyStatus.Active,
            effectiveAt,
            actorId,
            createdAt,
            CorrelationId,
            CancellationToken.None);

        // Then
        Assert.Equal(UsagePolicyCreateStatus.Created, result.Status);
        Assert.NotNull(result.Policy);
        Assert.Equal(1, result.Policy.Version);
        Assert.Equal(1_000_000, result.Policy.MonthlyTokenLimit);
        Assert.Equal(UsagePolicyStatus.Active, result.Policy.Status);
        Assert.Equal(effectiveAt, result.Policy.EffectiveAt);
        Assert.Equal(actorId, result.Policy.ActorId);

        var entry = Assert.Single(administrativeAuditRepository.StagedEntries);
        Assert.Equal(organization.Id, entry.OrganizationId);
        Assert.Equal(AdministrativeAuditAction.UsagePolicyChanged, entry.Action);
        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal(organization.Id, entry.TargetId);
        Assert.Equal(createdAt, entry.OccurredAt);
        Assert.Equal(CorrelationId, entry.CorrelationId);
        Assert.Equal("{}", entry.OldValues);
        Assert.Contains("\"monthlyTokenLimit\":1000000", entry.NewValues);
        Assert.Contains("\"version\":1", entry.NewValues);
    }

    [Fact]
    public async Task Given_AnExistingPolicy_When_CreateAsync_Then_CreatesTheNextVersionWithOldAndNewValues()
    {
        // Given
        await using var dbContext = CreateDbContext();
        var organization = await SeedOrganizationAsync(dbContext);
        var administrativeAuditRepository = new RecordingAdministrativeAuditRepository();
        var repository = new OrganizationUsagePolicyRepository(dbContext, administrativeAuditRepository);
        await repository.CreateAsync(
            organization.Id,
            500_000,
            UsagePolicyStatus.Active,
            DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-07-18T15:00:00Z"),
            CorrelationId,
            CancellationToken.None);

        // When
        var result = await repository.CreateAsync(
            organization.Id,
            1_000_000,
            UsagePolicyStatus.Active,
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-08-18T15:00:00Z"),
            CorrelationId,
            CancellationToken.None);

        // Then
        Assert.Equal(UsagePolicyCreateStatus.Created, result.Status);
        Assert.Equal(2, result.Policy!.Version);

        var entry = administrativeAuditRepository.StagedEntries[1];
        Assert.Contains("\"monthlyTokenLimit\":500000", entry.OldValues);
        Assert.Contains("\"version\":1", entry.OldValues);
        Assert.Contains("\"monthlyTokenLimit\":1000000", entry.NewValues);
        Assert.Contains("\"version\":2", entry.NewValues);
    }

    [Fact]
    public async Task Given_MultipleVersions_When_FindEffectiveAsync_Then_ReturnsTheLatestVersionAlreadyInEffect()
    {
        // Given
        await using var dbContext = CreateDbContext();
        var organization = await SeedOrganizationAsync(dbContext);
        var repository = new OrganizationUsagePolicyRepository(dbContext, new StubAdministrativeAuditRepository());
        await repository.CreateAsync(
            organization.Id,
            500_000,
            UsagePolicyStatus.Active,
            DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-07-18T15:00:00Z"),
            CorrelationId,
            CancellationToken.None);
        await repository.CreateAsync(
            organization.Id,
            1_000_000,
            UsagePolicyStatus.Active,
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-08-18T15:00:00Z"),
            CorrelationId,
            CancellationToken.None);

        // When
        var midAugust = await repository.FindEffectiveAsync(
            organization.Id,
            DateTimeOffset.Parse("2026-08-18T00:00:00Z"),
            CancellationToken.None);
        var afterSeptemberFirst = await repository.FindEffectiveAsync(
            organization.Id,
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            CancellationToken.None);

        // Then
        Assert.Equal(1, midAugust!.Version);
        Assert.Equal(500_000, midAugust.MonthlyTokenLimit);
        Assert.Equal(2, afterSeptemberFirst!.Version);
        Assert.Equal(1_000_000, afterSeptemberFirst.MonthlyTokenLimit);
    }

    [Fact]
    public async Task Given_NoPolicyForTheOrganization_When_FindEffectiveAsync_Then_ReturnsNull()
    {
        // Given
        await using var dbContext = CreateDbContext();
        var repository = new OrganizationUsagePolicyRepository(dbContext, new StubAdministrativeAuditRepository());

        // When
        var result = await repository.FindEffectiveAsync(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Then
        Assert.Null(result);
    }

    [Fact]
    public async Task Given_APolicyThatHasNotTakenEffectYet_When_FindEffectiveAsync_Then_ReturnsNull()
    {
        // Given
        await using var dbContext = CreateDbContext();
        var organization = await SeedOrganizationAsync(dbContext);
        var repository = new OrganizationUsagePolicyRepository(dbContext, new StubAdministrativeAuditRepository());
        await repository.CreateAsync(
            organization.Id,
            1_000_000,
            UsagePolicyStatus.Active,
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-08-18T15:00:00Z"),
            CorrelationId,
            CancellationToken.None);

        // When
        var result = await repository.FindEffectiveAsync(
            organization.Id,
            DateTimeOffset.Parse("2026-08-18T15:00:01Z"),
            CancellationToken.None);

        // Then
        Assert.Null(result);
    }

    private static async Task<Organization> SeedOrganizationAsync(AssistantCoreDbContext dbContext)
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Contoso",
            Domain = $"{Guid.NewGuid()}.example.com",
            IdentityProvider = IdentityProvider.MicrosoftEntraId,
            Status = RecordStatus.Active
        };
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return organization;
    }

    private static AssistantCoreDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
