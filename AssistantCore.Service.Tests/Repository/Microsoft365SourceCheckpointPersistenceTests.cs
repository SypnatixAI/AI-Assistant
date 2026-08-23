using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class Microsoft365SourceCheckpointPersistenceTests
{
    [Theory]
    [InlineAutoDomainData("https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=opaque%2Bvalue%3D%3D&custom=value")]
    public async Task Given_ACompleteCheckpoint_When_SaveChangesAsync_Then_PersistsEveryValueAndKeepsDeltaLinkOpaque(
        string deltaLink,
        Guid databaseId,
        Guid sourceId,
        Guid connectionId,
        DateTimeOffset lastSuccessfulSynchronizationAt,
        DateTimeOffset lastSynchronizationAttemptAt,
        string lastErrorCode,
        Guid synchronizationLeaseId,
        DateTimeOffset synchronizationLeaseExpiresAt)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        dbContext.Microsoft365Sources.Add(new Microsoft365Source
        {
            Id = sourceId,
            Microsoft365ConnectionId = connectionId,
            Kind = Microsoft365SourceKind.SharePointList,
            ExternalResourceId = "list-id",
            DisplayName = "Requests",
            Status = Microsoft365SourceStatus.Enabled,
            DeltaLink = deltaLink,
            DiscoveredAt = lastSynchronizationAttemptAt.AddDays(-1),
            LastSuccessfulSynchronizationAt = lastSuccessfulSynchronizationAt,
            LastSynchronizationAttemptAt = lastSynchronizationAttemptAt,
            LastErrorCode = lastErrorCode,
            SynchronizationLeaseId = synchronizationLeaseId,
            SynchronizationLeaseExpiresAt = synchronizationLeaseExpiresAt
        });

        // When
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var persistedSource = await dbContext.Microsoft365Sources.SingleAsync();

        // Then
        Assert.Equal(deltaLink, persistedSource.DeltaLink);
        Assert.Equal(lastSuccessfulSynchronizationAt, persistedSource.LastSuccessfulSynchronizationAt);
        Assert.Equal(lastSynchronizationAttemptAt, persistedSource.LastSynchronizationAttemptAt);
        Assert.Equal(lastErrorCode, persistedSource.LastErrorCode);
        Assert.Equal(synchronizationLeaseId, persistedSource.SynchronizationLeaseId);
        Assert.Equal(synchronizationLeaseExpiresAt, persistedSource.SynchronizationLeaseExpiresAt);
    }

    private static AssistantCoreDbContext CreateDbContext(Guid databaseId) =>
        new(new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(databaseId.ToString())
            .Options);
}
