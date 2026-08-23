using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class Microsoft365SubscriptionRepositoryTests
{
    [Theory, AutoDomainData]
    public async Task Given_MixedSources_When_GetReconciliationCandidatesAsync_Then_ReturnsOnlyDueActiveSource(
        Guid databaseId)
    {
        // Given
        var now = DateTimeOffset.Parse("2026-08-22T12:00:00Z");
        await using var dbContext = CreateDbContext(databaseId);
        var dueSubscription = CreateSubscription(now, "due", now.AddMinutes(-1));
        var futureSubscription = CreateSubscription(now, "future", now.AddMinutes(1));
        var disabledSubscription = CreateSubscription(now, "disabled", null);
        disabledSubscription.Microsoft365Source.Status = Microsoft365SourceStatus.Disabled;
        var expiredSubscription = CreateSubscription(now, "expired", null);
        expiredSubscription.ExpiresAt = now;
        dbContext.AddRange(
            dueSubscription,
            futureSubscription,
            disabledSubscription,
            expiredSubscription);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new Microsoft365SubscriptionRepository(dbContext);

        // When
        var candidates = await repository.GetReconciliationCandidatesAsync(
            now,
            CancellationToken.None);

        // Then
        Assert.Equal(dueSubscription.Id, Assert.Single(candidates).Id);
    }

    private static Microsoft365Subscription CreateSubscription(
        DateTimeOffset now,
        string suffix,
        DateTimeOffset? nextSynchronizationAt)
    {
        var organizationId = Guid.NewGuid();
        var connector = new OrganizationConnector
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Type = ConnectorType.Microsoft365,
            Status = RecordStatus.Active,
            IsConfigured = true
        };
        var connection = new Microsoft365Connection
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OrganizationConnectorId = connector.Id,
            Status = Microsoft365ConnectionStatus.Active,
            OrganizationConnector = connector,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1)
        };
        connector.Microsoft365Connection = connection;
        var source = new Microsoft365List
        {
            Id = Guid.NewGuid(),
            Microsoft365ConnectionId = connection.Id,
            Microsoft365Connection = connection,
            OrganizationId = organizationId,
            OrganizationConnectorId = connector.Id,
            SiteId = $"site-{suffix}",
            ListId = $"list-{suffix}",
            Kind = Microsoft365SourceKind.SharePointList,
            ExternalResourceId = $"list-{suffix}",
            DisplayName = suffix,
            Status = Microsoft365SourceStatus.Enabled,
            IsIndexed = true,
            DeltaLink = $"delta-{suffix}",
            DiscoveredAt = now.AddDays(-1),
            NextSynchronizationAt = nextSynchronizationAt
        };
        connection.Sources.Add(source);
        var subscription = new Microsoft365Subscription
        {
            Id = Guid.NewGuid(),
            Microsoft365SourceId = source.Id,
            OrganizationId = organizationId,
            MicrosoftSubscriptionId = $"subscription-{suffix}",
            ProtectedClientState = "protected-client-state",
            ExpiresAt = now.AddHours(1),
            Status = Microsoft365SubscriptionStatus.Active,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1),
            Microsoft365Source = source
        };
        source.Subscriptions.Add(subscription);
        return subscription;
    }

    private static AssistantCoreDbContext CreateDbContext(Guid databaseId) =>
        new(new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(databaseId.ToString())
            .Options);
}
