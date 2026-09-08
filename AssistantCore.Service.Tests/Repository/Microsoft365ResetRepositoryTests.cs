using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class Microsoft365ResetRepositoryTests
{
    [Theory, AutoDomainData]
    public async Task Given_ConfiguredOrganization_When_ResetAsync_Then_SelectedSitesAreRemoved(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        var repository = new Microsoft365ResetRepository(dbContext);
        var discovery = new Microsoft365SourceDiscoveryRepository(dbContext);

        // When
        await repository.ResetSelectionAndIndexingAsync(
            alpha.OrganizationId,
            CancellationToken.None);

        // Then
        var siteIds = await discovery.GetSiteIdsAsync(alpha.OrganizationId, CancellationToken.None);
        Assert.Empty(siteIds);
        Assert.False(await discovery.HasIndexedSourceAsync(
            alpha.OrganizationId,
            CancellationToken.None));
    }

    [Theory, AutoDomainData]
    public async Task Given_ConfiguredOrganization_When_ResetAsync_Then_ConnectionIsPreserved(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        var repository = new Microsoft365ResetRepository(dbContext);

        // When
        await repository.ResetSelectionAndIndexingAsync(
            alpha.OrganizationId,
            CancellationToken.None);

        // Then
        var connection = await dbContext.Microsoft365Connections
            .SingleAsync(candidate => candidate.OrganizationId == alpha.OrganizationId);
        Assert.Equal(Microsoft365ConnectionStatus.Active, connection.Status);
        Assert.Equal(alpha.Connection.TenantId, connection.TenantId);
        Assert.Single(await dbContext.Organizations.ToListAsync());
    }

    [Theory, AutoDomainData]
    public async Task Given_ConfiguredOrganization_When_ResetAsync_Then_SubscriptionsAndWorkAreRemoved(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        var repository = new Microsoft365ResetRepository(dbContext);

        // When
        var counts = await repository.ResetSelectionAndIndexingAsync(
            alpha.OrganizationId,
            CancellationToken.None);

        // Then
        Assert.Equal(1, counts.Subscriptions);
        Assert.Equal(1, counts.Synchronizations);
        Assert.Equal(1, counts.DocumentWorks);
        Assert.Equal(1, counts.ListItemWorks);
        Assert.Empty(await dbContext.Microsoft365Subscriptions.ToListAsync());
        Assert.Empty(await dbContext.Microsoft365Synchronizations.ToListAsync());
        Assert.Empty(await dbContext.Microsoft365DocumentWorks.ToListAsync());
        Assert.Empty(await dbContext.Microsoft365ListItemWorks.ToListAsync());
    }

    [Theory, AutoDomainData]
    public async Task Given_IndexedContent_When_ResetAsync_Then_ContentAndPassagesAreDeleted(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        var repository = new Microsoft365ResetRepository(dbContext);

        // When
        var counts = await repository.ResetSelectionAndIndexingAsync(
            alpha.OrganizationId,
            CancellationToken.None);

        // Then
        Assert.Equal(1, counts.IndexedContents);
        Assert.Empty(await dbContext.Microsoft365IndexedContents.ToListAsync());
        Assert.Empty(await dbContext.Microsoft365IndexedPassages.ToListAsync());
    }

    [Theory, AutoDomainData]
    public async Task Given_IndexedContent_When_GetIndexedChunkIdsAsync_Then_ReturnsOnlyItsOwnChunks(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        await SeedOrganizationAsync(dbContext, "Beta");
        var repository = new Microsoft365ResetRepository(dbContext);

        // When
        var chunkIds = await repository.GetIndexedChunkIdsAsync(
            alpha.OrganizationId,
            CancellationToken.None);

        // Then
        Assert.Equal(["chunk-Alpha"], chunkIds);
    }

    [Theory, AutoDomainData]
    public async Task Given_ResetAlreadyCompleted_When_ResetAsync_Then_OperationIsIdempotent(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        var repository = new Microsoft365ResetRepository(dbContext);
        await repository.ResetSelectionAndIndexingAsync(
            alpha.OrganizationId,
            CancellationToken.None);

        // When
        var secondRun = await repository.ResetSelectionAndIndexingAsync(
            alpha.OrganizationId,
            CancellationToken.None);

        // Then
        Assert.Equal(0, secondRun.Subscriptions);
        Assert.Equal(0, secondRun.Synchronizations);
        Assert.Equal(0, secondRun.DocumentWorks);
        Assert.Equal(0, secondRun.ListItemWorks);
        Assert.Equal(0, secondRun.IndexedContents);
        Assert.Equal(0, secondRun.Sources);
    }

    [Theory, AutoDomainData]
    public async Task Given_OtherOrganizationData_When_ResetAsync_Then_OtherOrganizationIsUntouched(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        var beta = await SeedOrganizationAsync(dbContext, "Beta");
        var repository = new Microsoft365ResetRepository(dbContext);
        var discovery = new Microsoft365SourceDiscoveryRepository(dbContext);

        // When
        await repository.ResetSelectionAndIndexingAsync(
            alpha.OrganizationId,
            CancellationToken.None);

        // Then
        Assert.Empty(await discovery.GetSiteIdsAsync(alpha.OrganizationId, CancellationToken.None));
        Assert.Single(await discovery.GetSiteIdsAsync(beta.OrganizationId, CancellationToken.None));
        Assert.Single(await dbContext.Microsoft365Subscriptions.ToListAsync());
        Assert.Single(await dbContext.Microsoft365Synchronizations.ToListAsync());
        Assert.Single(await dbContext.Microsoft365DocumentWorks.ToListAsync());
        Assert.Single(await dbContext.Microsoft365ListItemWorks.ToListAsync());
        Assert.Single(await dbContext.Microsoft365IndexedContents.ToListAsync());
        Assert.Single(await dbContext.Microsoft365IndexedPassages.ToListAsync());
        Assert.Equal(
            beta.Drive.Id,
            (await dbContext.Microsoft365Drives.SingleAsync()).Id);
    }

    private sealed record SeededOrganization(
        Guid OrganizationId,
        Microsoft365Connection Connection,
        Microsoft365Site Site,
        Microsoft365Drive Drive);

    private static async Task<SeededOrganization> SeedOrganizationAsync(
        AssistantCoreDbContext dbContext,
        string name)
    {
        var organizationId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var organization = new Organization { Id = organizationId, Name = name };
        var connector = new OrganizationConnector
        {
            Id = connectorId,
            OrganizationId = organizationId,
            Type = ConnectorType.Microsoft365,
            Status = RecordStatus.Active,
            IsConfigured = true
        };
        var connection = new Microsoft365Connection
        {
            Id = connectionId,
            OrganizationId = organizationId,
            OrganizationConnectorId = connectorId,
            TenantId = $"tenant-{name}",
            Status = Microsoft365ConnectionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        var site = new Microsoft365Site
        {
            Id = Guid.NewGuid(),
            Microsoft365ConnectionId = connectionId,
            OrganizationId = organizationId,
            OrganizationConnectorId = connectorId,
            SiteId = $"site-{name}",
            Kind = Microsoft365SourceKind.SharePointSite,
            ExternalResourceId = $"site-{name}",
            DisplayName = $"Site {name}",
            Status = Microsoft365SourceStatus.Enabled,
            IsIndexed = false,
            DiscoveredAt = now
        };
        var drive = new Microsoft365Drive
        {
            Id = Guid.NewGuid(),
            Microsoft365ConnectionId = connectionId,
            OrganizationId = organizationId,
            OrganizationConnectorId = connectorId,
            SiteId = $"site-{name}",
            DriveId = $"drive-{name}",
            Kind = Microsoft365SourceKind.SharePointDrive,
            ExternalResourceId = $"drive-{name}",
            ParentExternalResourceId = $"site-{name}",
            DisplayName = $"Lecteur {name}",
            Status = Microsoft365SourceStatus.Enabled,
            IsIndexed = true,
            DiscoveredAt = now
        };
        var subscription = new Microsoft365Subscription
        {
            Id = Guid.NewGuid(),
            Microsoft365SourceId = drive.Id,
            OrganizationId = organizationId,
            MicrosoftSubscriptionId = $"subscription-{name}",
            Status = Microsoft365SubscriptionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        var synchronization = new Microsoft365Synchronization
        {
            Id = Guid.NewGuid(),
            Microsoft365SourceId = drive.Id,
            Type = Microsoft365SynchronizationType.Initial,
            Status = Microsoft365SynchronizationStatus.Succeeded,
            RequestedAt = now
        };
        var documentWork = new Microsoft365DocumentWork
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Microsoft365SourceId = drive.Id,
            Microsoft365SynchronizationId = synchronization.Id,
            SiteId = $"site-{name}",
            DriveId = $"drive-{name}",
            DriveItemId = $"item-{name}",
            WorkType = Microsoft365DocumentWorkType.ProcessDocument,
            Status = Microsoft365DocumentWorkStatus.Pending,
            DeduplicationKey = $"dedup-document-{name}",
            CreatedAt = now
        };
        var listItemWork = new Microsoft365ListItemWork
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Microsoft365SourceId = drive.Id,
            Microsoft365SynchronizationId = synchronization.Id,
            SiteId = $"site-{name}",
            ListId = $"list-{name}",
            ListItemId = $"list-item-{name}",
            WorkType = Microsoft365ListItemWorkType.ProcessListItem,
            DeduplicationKey = $"dedup-list-{name}",
            CreatedAt = now
        };
        var indexedContent = new Microsoft365IndexedContent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Microsoft365SourceId = drive.Id,
            ExternalContentId = $"content-{name}",
            IsAvailable = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var passage = new Microsoft365IndexedPassage
        {
            Id = Guid.NewGuid(),
            Microsoft365IndexedContentId = indexedContent.Id,
            ChunkId = $"chunk-{name}"
        };

        dbContext.AddRange(
            organization,
            connector,
            connection,
            site,
            drive,
            subscription,
            synchronization,
            documentWork,
            listItemWork,
            indexedContent,
            passage);
        await dbContext.SaveChangesAsync();

        return new SeededOrganization(organizationId, connection, site, drive);
    }

    private static AssistantCoreDbContext CreateDbContext(Guid databaseId)
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(databaseId.ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
