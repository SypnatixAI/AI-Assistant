using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class TokenConsumptionRepositoryTests
{
    [Theory, AutoDomainData]
    public async Task Given_ANewConsumption_When_TryRecordConsumptionAsync_Then_PersistsItAndReturnsTrue(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var (organization, message) = await SeedOrganizationAndMessageAsync(dbContext);
        var repository = new TokenConsumptionRepository(dbContext);
        var consumption = CreateConsumption(organization.Id, message.Id);

        // When
        var recorded = await repository.TryRecordConsumptionAsync(consumption, CancellationToken.None);

        // Then
        Assert.True(recorded);
        var persisted = await dbContext.TokenConsumptions.SingleAsync();
        Assert.Equal(consumption.Id, persisted.Id);
        Assert.Equal(consumption.TotalTokens, persisted.TotalTokens);
    }

    [Theory, AutoDomainData]
    public async Task Given_MultipleConsumptionsAcrossPeriodsAndOrganizations_When_SumTokensForPeriodAsync_Then_SumsOnlyTheMatchingOrganizationAndPeriod(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var (organizationA, messageA1) = await SeedOrganizationAndMessageAsync(dbContext);
        var (_, messageA2) = await SeedOrganizationAndMessageAsync(dbContext, organizationA);
        var (organizationB, messageB1) = await SeedOrganizationAndMessageAsync(dbContext);
        var periodStartsAt = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var periodEndsAt = DateTimeOffset.Parse("2026-09-01T00:00:00Z");
        var otherPeriodStartsAt = DateTimeOffset.Parse("2026-09-01T00:00:00Z");
        var otherPeriodEndsAt = DateTimeOffset.Parse("2026-10-01T00:00:00Z");

        dbContext.TokenConsumptions.AddRange(
            CreateConsumption(organizationA.Id, messageA1.Id, periodStartsAt, periodEndsAt, totalTokens: 100),
            CreateConsumption(organizationA.Id, messageA2.Id, periodStartsAt, periodEndsAt, totalTokens: 50),
            CreateConsumption(organizationA.Id, messageA1.Id, otherPeriodStartsAt, otherPeriodEndsAt, totalTokens: 999),
            CreateConsumption(organizationB.Id, messageB1.Id, periodStartsAt, periodEndsAt, totalTokens: 777));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new TokenConsumptionRepository(dbContext);

        // When
        var total = await repository.SumTokensForPeriodAsync(
            organizationA.Id,
            periodStartsAt,
            periodEndsAt,
            CancellationToken.None);

        // Then
        Assert.Equal(150, total);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoConsumptionForThePeriod_When_SumTokensForPeriodAsync_Then_ReturnsZero(
        Guid databaseId,
        Guid organizationId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var repository = new TokenConsumptionRepository(dbContext);

        // When
        var total = await repository.SumTokensForPeriodAsync(
            organizationId,
            DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            CancellationToken.None);

        // Then
        Assert.Equal(0, total);
    }

    private static TokenConsumption CreateConsumption(
        Guid organizationId,
        Guid assistantMessageId,
        DateTimeOffset? periodStartsAt = null,
        DateTimeOffset? periodEndsAt = null,
        long totalTokens = 120) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            AssistantMessageId = assistantMessageId,
            PeriodStartsAt = periodStartsAt ?? DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            PeriodEndsAt = periodEndsAt ?? DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            InputTokens = totalTokens - 20,
            OutputTokens = 20,
            TotalTokens = totalTokens,
            CreatedAt = DateTimeOffset.Parse("2026-08-18T12:00:00Z")
        };

    private static async Task<(Organization Organization, Message Message)> SeedOrganizationAndMessageAsync(
        AssistantCoreDbContext dbContext,
        Organization? organization = null)
    {
        organization ??= new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Contoso",
            Domain = $"{Guid.NewGuid()}.example.com",
            IdentityProvider = IdentityProvider.MicrosoftEntraId,
            Status = RecordStatus.Active
        };
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            OwnerMemberId = Guid.NewGuid(),
            Title = "Conversation",
            Status = ConversationStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = MessageRole.Assistant,
            Content = "Answer",
            ProcessingStatus = MessageProcessingStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (dbContext.Entry(organization).State == EntityState.Detached
            && !await dbContext.Organizations.AnyAsync(candidate => candidate.Id == organization.Id))
        {
            dbContext.Organizations.Add(organization);
        }

        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return (organization, message);
    }

    private static AssistantCoreDbContext CreateDbContext(Guid databaseId) =>
        new(new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(databaseId.ToString())
            .Options);
}
