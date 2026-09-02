using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class Microsoft365SourceDiscoveryRepositoryTests
{
    [Theory, AutoDomainData]
    public async Task Given_ARequestedOrganization_When_FindSiteAsync_Then_ReturnsOnlyItsSite(
        Guid databaseId,
        Guid foreignOrganizationId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var site = await SeedSiteAsync(dbContext);
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);

        // When
        var ownedSite = await repository.FindSiteAsync(
            site.OrganizationId,
            site.SiteId,
            CancellationToken.None);
        var foreignSite = await repository.FindSiteAsync(
            foreignOrganizationId,
            site.SiteId,
            CancellationToken.None);

        // Then
        Assert.Same(site, ownedSite);
        Assert.Null(foreignSite);
    }

    [Theory, AutoDomainData]
    public async Task Given_ListsFromMultipleSites_When_GetListsAsync_Then_ReturnsOnlyRequestedOrganizationSiteLists(
        Guid databaseId,
        Guid foreignOrganizationId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var site = await SeedSiteAsync(dbContext);
        var requestedList = CreateList(site, "requested-list", "Requests");
        var otherSiteList = CreateList(site, "other-list", "Other site");
        otherSiteList.SiteId = "other-site";
        dbContext.AddRange(requestedList, otherSiteList);
        await dbContext.SaveChangesAsync();
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);

        // When
        var lists = await repository.GetListsAsync(
            site.OrganizationId,
            site.SiteId,
            CancellationToken.None);
        var foreignLists = await repository.GetListsAsync(
            foreignOrganizationId,
            site.SiteId,
            CancellationToken.None);

        // Then
        Assert.Equal(requestedList.Id, Assert.Single(lists).Id);
        Assert.Empty(foreignLists);
    }

    [Theory, AutoDomainData]
    public async Task Given_OnlyAnUnavailableIndexedSource_When_HasIndexedSourceAsync_Then_ReturnsFalse(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var site = await SeedSiteAsync(dbContext);
        var list = CreateList(site, "list-id", "Requests");
        list.EnableIndexing(DateTimeOffset.UtcNow);
        list.MarkUnavailable();
        dbContext.Add(list);
        await dbContext.SaveChangesAsync();
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);

        // When
        var hasIndexedSource = await repository.HasIndexedSourceAsync(
            site.OrganizationId,
            CancellationToken.None);

        // Then
        Assert.False(hasIndexedSource);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheSameListActivationTwice_When_SaveListActivationAsync_Then_CreatesOneUsefulWorkAndSubscription(
        Guid databaseId,
        DateTimeOffset requestedAt)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var site = await SeedSiteAsync(dbContext);
        var list = CreateList(site, "list-id", "Requests");
        dbContext.Add(list);
        await dbContext.SaveChangesAsync();
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);
        var loadedList = await repository.FindListAsync(
            site.OrganizationId,
            site.SiteId,
            list.ListId,
            CancellationToken.None);
        Assert.NotNull(loadedList);
        loadedList.EnableIndexing(requestedAt);

        // When
        await repository.SaveListActivationAsync(loadedList, requestedAt, CancellationToken.None);
        await repository.SaveListActivationAsync(loadedList, requestedAt, CancellationToken.None);

        // Then
        var synchronization = Assert.Single(await dbContext.Microsoft365Synchronizations.ToListAsync());
        Assert.Equal(Microsoft365SynchronizationType.Initial, synchronization.Type);
        Assert.Equal(Microsoft365SynchronizationStatus.Pending, synchronization.Status);
        var subscription = Assert.Single(await dbContext.Microsoft365Subscriptions.ToListAsync());
        Assert.Equal(Microsoft365SubscriptionStatus.Pending, subscription.Status);
        Assert.Equal(site.OrganizationId, subscription.OrganizationId);
        Assert.Equal($"/sites/{site.SiteId}/lists/{list.ListId}", subscription.Resource);
        Assert.Null(subscription.MicrosoftSubscriptionId);
        Assert.Null(subscription.ProtectedClientState);
        Assert.Null(subscription.ExpiresAt);
    }

    [Theory, AutoDomainData]
    public async Task Given_APermanentlyFailedInitialListSynchronization_When_SaveListActivationAsync_Then_CreatesANewRetry(
        Guid databaseId,
        DateTimeOffset requestedAt)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var site = await SeedSiteAsync(dbContext);
        var list = CreateList(site, "list-id", "Requests");
        list.EnableIndexing(requestedAt.AddMinutes(-1));
        list.Synchronizations.Add(new Microsoft365Synchronization
        {
            Id = Guid.NewGuid(),
            Microsoft365SourceId = list.Id,
            Type = Microsoft365SynchronizationType.Initial,
            Status = Microsoft365SynchronizationStatus.PermanentFailure,
            RequestedAt = requestedAt.AddMinutes(-1)
        });
        dbContext.Add(list);
        await dbContext.SaveChangesAsync();
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);
        var loadedList = await repository.FindListAsync(
            site.OrganizationId,
            site.SiteId,
            list.ListId,
            CancellationToken.None);
        Assert.NotNull(loadedList);

        // When
        var result = await repository.SaveListActivationAsync(
            loadedList,
            requestedAt,
            CancellationToken.None);

        // Then
        Assert.Equal(1, result.InitialSynchronizationRequests);
        Assert.Equal(2, loadedList.Synchronizations.Count);
        Assert.Contains(
            loadedList.Synchronizations,
            synchronization => synchronization.Status == Microsoft365SynchronizationStatus.Pending);
    }

    [Theory, AutoDomainData]
    public async Task Given_APermanentlyFailedInitialDriveSynchronization_When_SaveDriveActivationAsync_Then_CreatesANewRetry(
        Guid databaseId,
        DateTimeOffset requestedAt)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var site = await SeedSiteAsync(dbContext);
        var drive = CreateDrive(site, "drive-id", "Documents");
        drive.EnableIndexing(requestedAt.AddMinutes(-1));
        drive.Synchronizations.Add(new Microsoft365Synchronization
        {
            Id = Guid.NewGuid(),
            Microsoft365SourceId = drive.Id,
            Type = Microsoft365SynchronizationType.Initial,
            Status = Microsoft365SynchronizationStatus.PermanentFailure,
            RequestedAt = requestedAt.AddMinutes(-1)
        });
        dbContext.Add(drive);
        await dbContext.SaveChangesAsync();
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);
        var loadedDrive = await repository.FindDriveAsync(
            site.OrganizationId,
            site.SiteId,
            drive.DriveId,
            CancellationToken.None);
        Assert.NotNull(loadedDrive);

        // When
        await repository.SaveDriveActivationAsync(
            loadedDrive,
            requestedAt,
            CancellationToken.None);

        // Then
        Assert.Equal(2, loadedDrive.Synchronizations.Count);
        Assert.Contains(
            loadedDrive.Synchronizations,
            synchronization => synchronization.Status == Microsoft365SynchronizationStatus.Pending);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEnabledList_When_SaveListDeactivationAsync_Then_CancelsIngestionAndRequestsOneCleanup(
        Guid databaseId,
        string microsoftSubscriptionId,
        DateTimeOffset requestedAt)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var site = await SeedSiteAsync(dbContext);
        var list = CreateList(site, "list-id", "Requests");
        dbContext.Add(list);
        await dbContext.SaveChangesAsync();
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);
        var loadedList = await repository.FindListAsync(
            site.OrganizationId,
            site.SiteId,
            list.ListId,
            CancellationToken.None);
        Assert.NotNull(loadedList);
        loadedList.EnableIndexing(requestedAt.AddMinutes(-1));
        await repository.SaveListActivationAsync(
            loadedList,
            requestedAt.AddMinutes(-1),
            CancellationToken.None);
        var subscription = Assert.Single(loadedList.Subscriptions);
        subscription.MicrosoftSubscriptionId = microsoftSubscriptionId;
        subscription.ProtectedClientState = "protected-state";
        subscription.ExpiresAt = requestedAt.AddHours(1);
        subscription.Status = Microsoft365SubscriptionStatus.Active;
        loadedList.Synchronizations.Add(new Microsoft365Synchronization
        {
            Id = Guid.NewGuid(),
            Microsoft365SourceId = loadedList.Id,
            Type = Microsoft365SynchronizationType.Delta,
            Status = Microsoft365SynchronizationStatus.TemporaryFailure,
            RequestedAt = requestedAt.AddMinutes(-1)
        });
        await dbContext.SaveChangesAsync();
        loadedList.DisableIndexing();

        // When
        var firstResult = await repository.SaveListDeactivationAsync(
            loadedList,
            requestedAt,
            requestIndexCleanup: true,
            CancellationToken.None);
        var secondResult = await repository.SaveListDeactivationAsync(
            loadedList,
            requestedAt,
            requestIndexCleanup: true,
            CancellationToken.None);

        // Then
        Assert.False(loadedList.IsIndexed);
        Assert.Equal(Microsoft365SourceStatus.Disabled, loadedList.Status);
        Assert.Equal(2, firstResult.CancelledIngestionJobs);
        Assert.Equal(1, firstResult.SubscriptionStopRequests);
        Assert.Equal(1, firstResult.IndexCleanupRequests);
        Assert.Equal(Microsoft365ListIndexingRequestCounts.Empty, secondResult);
        Assert.Equal(Microsoft365SubscriptionStatus.RevocationRequired, subscription.Status);
        Assert.Equal(2, loadedList.Synchronizations.Count(synchronization =>
            synchronization.Status == Microsoft365SynchronizationStatus.Cancelled));
        var cleanup = Assert.Single(
            loadedList.Synchronizations,
            synchronization => synchronization.Type == Microsoft365SynchronizationType.IndexCleanup);
        Assert.Equal(Microsoft365SynchronizationStatus.Pending, cleanup.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheSameGraphSourcesTwice_When_ReconcileSiteSourcesAsync_Then_UpdatesWithoutDuplicates(
        Guid databaseId,
        DateTimeOffset discoveredAt)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var site = await SeedSiteAsync(dbContext);
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);
        var drives = new[] { new Microsoft365SourceDiscoveryData("drive-id", "Documents", null) };
        var lists = new[] { new Microsoft365SourceDiscoveryData("list-id", "Requests", null) };

        // When
        await repository.ReconcileSiteSourcesAsync(site, drives, lists, discoveredAt);
        await repository.ReconcileSiteSourcesAsync(
            site,
            [new Microsoft365SourceDiscoveryData("drive-id", "Updated documents", "https://drive")],
            [new Microsoft365SourceDiscoveryData("list-id", "Updated requests", "https://list")],
            discoveredAt.AddMinutes(1));

        // Then
        var drive = Assert.Single(await dbContext.Microsoft365Drives.ToListAsync());
        Assert.Equal("Updated documents", drive.DisplayName);
        Assert.Equal("https://drive", drive.WebUrl);
        Assert.Equal(Microsoft365SourceStatus.Discovered, drive.Status);
        Assert.False(drive.IsIndexed);

        var list = Assert.Single(await dbContext.Microsoft365Lists.ToListAsync());
        Assert.Equal("Updated requests", list.DisplayName);
        Assert.Equal("https://list", list.WebUrl);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEnabledSourceMissingFromGraph_When_ReconcileSiteSourcesAsync_Then_MarksUnavailableAndPreservesHistory(
        Guid databaseId,
        DateTimeOffset discoveredAt)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var site = await SeedSiteAsync(dbContext);
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);
        await repository.ReconcileSiteSourcesAsync(
            site,
            [],
            [new Microsoft365SourceDiscoveryData("list-id", "Requests", null)],
            discoveredAt);
        var list = await dbContext.Microsoft365Lists.SingleAsync();
        list.Status = Microsoft365SourceStatus.Enabled;
        list.IsIndexed = true;
        dbContext.Microsoft365Synchronizations.Add(new Microsoft365Synchronization
        {
            Id = Guid.NewGuid(),
            Microsoft365SourceId = list.Id,
            Type = Microsoft365SynchronizationType.Initial,
            Status = Microsoft365SynchronizationStatus.Succeeded,
            RequestedAt = discoveredAt
        });
        await dbContext.SaveChangesAsync();

        // When
        await repository.ReconcileSiteSourcesAsync(site, [], [], discoveredAt.AddMinutes(1));

        // Then
        Assert.Equal(Microsoft365SourceStatus.Unavailable, list.Status);
        Assert.Equal(Microsoft365SourceStatus.Enabled, list.StatusBeforeUnavailable);
        Assert.True(list.IsIndexed);
        Assert.Single(await dbContext.Microsoft365Synchronizations.ToListAsync());
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnavailableSourceReturnedByGraph_When_ReconcileSiteSourcesAsync_Then_RestoresPreviousStatus(
        Guid databaseId,
        DateTimeOffset discoveredAt)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var site = await SeedSiteAsync(dbContext);
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);
        await repository.ReconcileSiteSourcesAsync(
            site,
            [new Microsoft365SourceDiscoveryData("drive-id", "Documents", null)],
            [],
            discoveredAt);
        var drive = await dbContext.Microsoft365Drives.SingleAsync();
        drive.Status = Microsoft365SourceStatus.Disabled;
        drive.MarkUnavailable();
        await dbContext.SaveChangesAsync();

        // When
        await repository.ReconcileSiteSourcesAsync(
            site,
            [new Microsoft365SourceDiscoveryData("drive-id", "Documents restored", null)],
            [],
            discoveredAt.AddMinutes(1));

        // Then
        Assert.Equal(Microsoft365SourceStatus.Disabled, drive.Status);
        Assert.Null(drive.StatusBeforeUnavailable);
        Assert.Equal("Documents restored", drive.DisplayName);
        Assert.Single(await dbContext.Microsoft365Drives.ToListAsync());
    }

    private static AssistantCoreDbContext CreateDbContext(Guid databaseId) =>
        new(new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(databaseId.ToString())
            .Options);

    private static async Task<Microsoft365Site> SeedSiteAsync(AssistantCoreDbContext dbContext)
    {
        var organizationId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var organization = new Organization { Id = organizationId, Name = "Organization" };
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
            Status = Microsoft365ConnectionStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var site = new Microsoft365Site
        {
            Id = Guid.NewGuid(),
            Microsoft365ConnectionId = connectionId,
            OrganizationId = organizationId,
            OrganizationConnectorId = connectorId,
            SiteId = "site-id",
            Kind = Microsoft365SourceKind.SharePointSite,
            ExternalResourceId = "site-id",
            DisplayName = "Site",
            Status = Microsoft365SourceStatus.Enabled,
            IsIndexed = false,
            DiscoveredAt = DateTimeOffset.UtcNow
        };

        dbContext.AddRange(organization, connector, connection, site);
        await dbContext.SaveChangesAsync();
        return site;
    }

    private static Microsoft365List CreateList(
        Microsoft365Site site,
        string listId,
        string displayName) =>
        new()
        {
            Id = Guid.NewGuid(),
            Microsoft365ConnectionId = site.Microsoft365ConnectionId,
            OrganizationId = site.OrganizationId,
            OrganizationConnectorId = site.OrganizationConnectorId,
            SiteId = site.SiteId,
            ListId = listId,
            Kind = Microsoft365SourceKind.SharePointList,
            ExternalResourceId = listId,
            ParentExternalResourceId = site.SiteId,
            DisplayName = displayName,
            Status = Microsoft365SourceStatus.Discovered,
            IsIndexed = false,
            DiscoveredAt = DateTimeOffset.UtcNow
        };

    private static Microsoft365Drive CreateDrive(
        Microsoft365Site site,
        string driveId,
        string displayName) =>
        new()
        {
            Id = Guid.NewGuid(),
            Microsoft365ConnectionId = site.Microsoft365ConnectionId,
            OrganizationId = site.OrganizationId,
            OrganizationConnectorId = site.OrganizationConnectorId,
            SiteId = site.SiteId,
            DriveId = driveId,
            Kind = Microsoft365SourceKind.SharePointDrive,
            ExternalResourceId = driveId,
            ParentExternalResourceId = site.SiteId,
            DisplayName = displayName,
            Status = Microsoft365SourceStatus.Discovered,
            IsIndexed = false,
            DiscoveredAt = DateTimeOffset.UtcNow
        };
}
