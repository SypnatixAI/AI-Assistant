using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Repositories;

public sealed class Microsoft365SourceDiscoveryRepository(AssistantCoreDbContext dbContext)
    : IMicrosoft365SourceDiscoveryRepository
{
    public async Task<Microsoft365Site> SaveSiteAsync(
        Microsoft365Connection connection,
        string siteId,
        string displayName,
        string webUrl,
        DateTimeOffset discoveredAt,
        CancellationToken cancellationToken = default)
    {
        var site = await dbContext.Microsoft365Sites.SingleOrDefaultAsync(candidate =>
            candidate.OrganizationId == connection.OrganizationId
            && candidate.SiteId == siteId,
            cancellationToken);
        if (site is null)
        {
            site = new Microsoft365Site
            {
                Id = Guid.NewGuid(),
                Microsoft365ConnectionId = connection.Id,
                OrganizationId = connection.OrganizationId,
                OrganizationConnectorId = connection.OrganizationConnectorId,
                SiteId = siteId,
                Kind = Microsoft365SourceKind.SharePointSite,
                ExternalResourceId = siteId,
                DisplayName = displayName,
                WebUrl = webUrl,
                Status = Microsoft365SourceStatus.Enabled,
                IsIndexed = false,
                DiscoveredAt = discoveredAt,
                EnabledAt = discoveredAt
            };
            dbContext.Microsoft365Sites.Add(site);
        }
        else
        {
            site.RefreshDiscovery(displayName, webUrl);
            site.Status = Microsoft365SourceStatus.Enabled;
            site.EnabledAt ??= discoveredAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return site;
    }

    public async Task<IReadOnlyCollection<Microsoft365Drive>> GetDrivesAsync(
        Guid organizationId,
        string siteId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Microsoft365Drives
            .AsNoTracking()
            .Where(drive => drive.OrganizationId == organizationId && drive.SiteId == siteId)
            .OrderBy(drive => drive.DisplayName)
            .ToArrayAsync(cancellationToken);

    public Task<Microsoft365Drive?> FindDriveAsync(
        Guid organizationId,
        string siteId,
        string driveId,
        CancellationToken cancellationToken = default) =>
        dbContext.Microsoft365Drives
            .Include(drive => drive.Microsoft365Connection)
            .Include(drive => drive.Synchronizations)
            .Include(drive => drive.Subscriptions)
            .SingleOrDefaultAsync(drive =>
                drive.OrganizationId == organizationId
                && drive.SiteId == siteId
                && drive.DriveId == driveId,
                cancellationToken);

    public async Task SaveDriveActivationAsync(
        Microsoft365Drive drive,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        if (drive.EnableIndexing(requestedAt)
            && !drive.Synchronizations.Any(synchronization =>
                synchronization.Type == Microsoft365SynchronizationType.Initial
                && synchronization.Status is Microsoft365SynchronizationStatus.Pending
                    or Microsoft365SynchronizationStatus.Running))
        {
            drive.Synchronizations.Add(new Microsoft365Synchronization
            {
                Id = Guid.NewGuid(),
                Microsoft365SourceId = drive.Id,
                Type = Microsoft365SynchronizationType.Initial,
                Status = Microsoft365SynchronizationStatus.Pending,
                RequestedAt = requestedAt
            });
        }

        if (!drive.Subscriptions.Any(subscription =>
                subscription.Status is Microsoft365SubscriptionStatus.Pending
                    or Microsoft365SubscriptionStatus.Active
                    or Microsoft365SubscriptionStatus.RenewalRequired))
        {
            drive.Subscriptions.Add(new Microsoft365Subscription
            {
                Id = Guid.NewGuid(),
                Microsoft365SourceId = drive.Id,
                OrganizationId = drive.OrganizationId,
                Resource = $"/drives/{drive.DriveId}/root",
                Status = Microsoft365SubscriptionStatus.Pending,
                CreatedAt = requestedAt,
                UpdatedAt = requestedAt
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveDriveDeactivationAsync(
        Microsoft365Drive drive,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        drive.DisableIndexing();
        foreach (var subscription in drive.Subscriptions.Where(subscription =>
                     subscription.Status is Microsoft365SubscriptionStatus.Pending
                         or Microsoft365SubscriptionStatus.Active
                         or Microsoft365SubscriptionStatus.RenewalRequired
                         or Microsoft365SubscriptionStatus.Error))
        {
            subscription.Status = string.IsNullOrWhiteSpace(subscription.MicrosoftSubscriptionId)
                ? Microsoft365SubscriptionStatus.Revoked
                : Microsoft365SubscriptionStatus.RevocationRequired;
            subscription.UpdatedAt = requestedAt;
        }
        foreach (var synchronization in drive.Synchronizations.Where(synchronization =>
                     synchronization.Status is Microsoft365SynchronizationStatus.Pending
                         or Microsoft365SynchronizationStatus.TemporaryFailure))
        {
            synchronization.Status = Microsoft365SynchronizationStatus.Cancelled;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public Task<Microsoft365Site?> FindSiteAsync(
        Guid organizationId,
        string siteId,
        CancellationToken cancellationToken = default) =>
        dbContext.Microsoft365Sites
            .Include(site => site.Microsoft365Connection)
            .Include(site => site.OrganizationConnector)
            .SingleOrDefaultAsync(site =>
                site.OrganizationId == organizationId
                && site.OrganizationConnector.OrganizationId == organizationId
                && site.OrganizationConnector.Type == ConnectorType.Microsoft365
                && site.Microsoft365Connection.OrganizationId == organizationId
                && site.Microsoft365Connection.OrganizationConnectorId == site.OrganizationConnectorId
                && site.SiteId == siteId,
                cancellationToken);

    public async Task<IReadOnlyCollection<Microsoft365List>> GetListsAsync(
        Guid organizationId,
        string siteId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Microsoft365Lists
            .AsNoTracking()
            .Where(list =>
                list.OrganizationId == organizationId
                && list.OrganizationConnector.OrganizationId == organizationId
                && list.OrganizationConnector.Type == ConnectorType.Microsoft365
                && list.Microsoft365Connection.OrganizationId == organizationId
                && list.Microsoft365Connection.OrganizationConnectorId == list.OrganizationConnectorId
                && list.SiteId == siteId)
            .OrderBy(list => list.DisplayName)
            .ThenBy(list => list.ListId)
            .ToListAsync(cancellationToken);

    public Task<Microsoft365List?> FindListAsync(
        Guid organizationId,
        string siteId,
        string listId,
        CancellationToken cancellationToken = default) =>
        dbContext.Microsoft365Lists
            .Include(list => list.Microsoft365Connection)
            .Include(list => list.OrganizationConnector)
            .Include(list => list.Subscriptions)
            .Include(list => list.Synchronizations)
            .SingleOrDefaultAsync(list =>
                list.OrganizationId == organizationId
                && list.OrganizationConnector.OrganizationId == organizationId
                && list.OrganizationConnector.Type == ConnectorType.Microsoft365
                && list.Microsoft365Connection.OrganizationId == organizationId
                && list.Microsoft365Connection.OrganizationConnectorId == list.OrganizationConnectorId
                && list.SiteId == siteId
                && list.ListId == listId,
                cancellationToken);

    public async Task<Microsoft365ListIndexingRequestCounts> SaveListActivationAsync(
        Microsoft365List list,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        var initialSynchronizationRequests = 0;
        var subscriptionCreationRequests = 0;

        if (!list.Synchronizations.Any(synchronization =>
                synchronization.Type == Microsoft365SynchronizationType.Initial
                && synchronization.Status is Microsoft365SynchronizationStatus.Pending
                    or Microsoft365SynchronizationStatus.Running))
        {
            list.Synchronizations.Add(new Microsoft365Synchronization
            {
                Id = Guid.NewGuid(),
                Microsoft365SourceId = list.Id,
                Type = Microsoft365SynchronizationType.Initial,
                Status = Microsoft365SynchronizationStatus.Pending,
                AttemptCount = 0,
                RequestedAt = requestedAt
            });
            initialSynchronizationRequests++;
        }

        if (!list.Subscriptions.Any(subscription =>
                subscription.Status is Microsoft365SubscriptionStatus.Pending
                    or Microsoft365SubscriptionStatus.Active
                    or Microsoft365SubscriptionStatus.RenewalRequired
                    or Microsoft365SubscriptionStatus.RevocationRequired))
        {
            list.Subscriptions.Add(new Microsoft365Subscription
            {
                Id = Guid.NewGuid(),
                Microsoft365SourceId = list.Id,
                OrganizationId = list.OrganizationId,
                Resource = $"/sites/{list.SiteId}/lists/{list.ListId}",
                Status = Microsoft365SubscriptionStatus.Pending,
                CreatedAt = requestedAt,
                UpdatedAt = requestedAt
            });
            subscriptionCreationRequests++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new Microsoft365ListIndexingRequestCounts(
            initialSynchronizationRequests,
            CancelledIngestionJobs: 0,
            subscriptionCreationRequests,
            SubscriptionStopRequests: 0,
            IndexCleanupRequests: 0);
    }

    public async Task<Microsoft365ListIndexingRequestCounts> SaveListDeactivationAsync(
        Microsoft365List list,
        DateTimeOffset requestedAt,
        bool requestIndexCleanup,
        CancellationToken cancellationToken = default)
    {
        var cancelledIngestionJobs = 0;
        var subscriptionStopRequests = 0;
        var indexCleanupRequests = 0;

        foreach (var synchronization in list.Synchronizations.Where(synchronization =>
                     synchronization.Type is Microsoft365SynchronizationType.Initial
                         or Microsoft365SynchronizationType.Delta
                     && synchronization.Status is Microsoft365SynchronizationStatus.Pending
                         or Microsoft365SynchronizationStatus.TemporaryFailure))
        {
            synchronization.Status = Microsoft365SynchronizationStatus.Cancelled;
            synchronization.CompletedAt = requestedAt;
            cancelledIngestionJobs++;
        }

        foreach (var subscription in list.Subscriptions.Where(subscription =>
                     subscription.Status is Microsoft365SubscriptionStatus.Pending
                         or Microsoft365SubscriptionStatus.Active
                         or Microsoft365SubscriptionStatus.RenewalRequired
                         or Microsoft365SubscriptionStatus.Error))
        {
            subscription.Status = string.IsNullOrWhiteSpace(subscription.MicrosoftSubscriptionId)
                ? Microsoft365SubscriptionStatus.Revoked
                : Microsoft365SubscriptionStatus.RevocationRequired;
            subscription.UpdatedAt = requestedAt;
            subscription.LastErrorCode = null;

            if (subscription.Status == Microsoft365SubscriptionStatus.RevocationRequired)
            {
                subscriptionStopRequests++;
            }
        }

        var hasUsefulIndexCleanup = list.Synchronizations.Any(synchronization =>
            synchronization.Type == Microsoft365SynchronizationType.IndexCleanup
            && synchronization.Status is Microsoft365SynchronizationStatus.Pending
                or Microsoft365SynchronizationStatus.Running
                or Microsoft365SynchronizationStatus.TemporaryFailure);
        if (requestIndexCleanup && !hasUsefulIndexCleanup)
        {
            list.Synchronizations.Add(new Microsoft365Synchronization
            {
                Id = Guid.NewGuid(),
                Microsoft365SourceId = list.Id,
                Type = Microsoft365SynchronizationType.IndexCleanup,
                Status = Microsoft365SynchronizationStatus.Pending,
                AttemptCount = 0,
                RequestedAt = requestedAt
            });
            indexCleanupRequests++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new Microsoft365ListIndexingRequestCounts(
            InitialSynchronizationRequests: 0,
            cancelledIngestionJobs,
            SubscriptionCreationRequests: 0,
            subscriptionStopRequests,
            indexCleanupRequests);
    }

    public async Task ReconcileSiteSourcesAsync(
        Microsoft365Site site,
        IReadOnlyCollection<Microsoft365SourceDiscoveryData> drives,
        IReadOnlyCollection<Microsoft365SourceDiscoveryData> lists,
        DateTimeOffset discoveredAt,
        CancellationToken cancellationToken = default)
    {
        var existingDrives = await dbContext.Microsoft365Drives
            .Where(drive =>
                drive.OrganizationId == site.OrganizationId
                && drive.OrganizationConnectorId == site.OrganizationConnectorId
                && drive.Microsoft365ConnectionId == site.Microsoft365ConnectionId
                && drive.SiteId == site.SiteId)
            .ToListAsync(cancellationToken);
        var existingLists = await dbContext.Microsoft365Lists
            .Where(list =>
                list.OrganizationId == site.OrganizationId
                && list.OrganizationConnectorId == site.OrganizationConnectorId
                && list.Microsoft365ConnectionId == site.Microsoft365ConnectionId
                && list.SiteId == site.SiteId)
            .ToListAsync(cancellationToken);

        ReconcileDrives(site, existingDrives, drives, discoveredAt);
        ReconcileLists(site, existingLists, lists, discoveredAt);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void ReconcileDrives(
        Microsoft365Site site,
        IReadOnlyCollection<Microsoft365Drive> existingDrives,
        IReadOnlyCollection<Microsoft365SourceDiscoveryData> discoveredDrives,
        DateTimeOffset discoveredAt)
    {
        var existingById = existingDrives.ToDictionary(
            drive => drive.DriveId,
            StringComparer.Ordinal);
        var discoveredIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var discoveredDrive in DistinctByMicrosoftId(discoveredDrives))
        {
            discoveredIds.Add(discoveredDrive.MicrosoftResourceId);
            if (existingById.TryGetValue(discoveredDrive.MicrosoftResourceId, out var existingDrive))
            {
                existingDrive.RefreshDiscovery(discoveredDrive.DisplayName, discoveredDrive.WebUrl);
                continue;
            }

            dbContext.Microsoft365Drives.Add(new Microsoft365Drive
            {
                Id = Guid.NewGuid(),
                Microsoft365ConnectionId = site.Microsoft365ConnectionId,
                OrganizationId = site.OrganizationId,
                OrganizationConnectorId = site.OrganizationConnectorId,
                SiteId = site.SiteId,
                DriveId = discoveredDrive.MicrosoftResourceId,
                Kind = Microsoft365SourceKind.SharePointDrive,
                ExternalResourceId = discoveredDrive.MicrosoftResourceId,
                ParentExternalResourceId = site.SiteId,
                DisplayName = discoveredDrive.DisplayName,
                WebUrl = discoveredDrive.WebUrl,
                Status = Microsoft365SourceStatus.Discovered,
                IsIndexed = false,
                DiscoveredAt = discoveredAt
            });
        }

        foreach (var missingDrive in existingDrives.Where(drive => !discoveredIds.Contains(drive.DriveId)))
        {
            missingDrive.MarkUnavailable();
        }
    }

    private void ReconcileLists(
        Microsoft365Site site,
        IReadOnlyCollection<Microsoft365List> existingLists,
        IReadOnlyCollection<Microsoft365SourceDiscoveryData> discoveredLists,
        DateTimeOffset discoveredAt)
    {
        var existingById = existingLists.ToDictionary(
            list => list.ListId,
            StringComparer.Ordinal);
        var discoveredIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var discoveredList in DistinctByMicrosoftId(discoveredLists))
        {
            discoveredIds.Add(discoveredList.MicrosoftResourceId);
            if (existingById.TryGetValue(discoveredList.MicrosoftResourceId, out var existingList))
            {
                existingList.RefreshDiscovery(discoveredList.DisplayName, discoveredList.WebUrl);
                continue;
            }

            dbContext.Microsoft365Lists.Add(new Microsoft365List
            {
                Id = Guid.NewGuid(),
                Microsoft365ConnectionId = site.Microsoft365ConnectionId,
                OrganizationId = site.OrganizationId,
                OrganizationConnectorId = site.OrganizationConnectorId,
                SiteId = site.SiteId,
                ListId = discoveredList.MicrosoftResourceId,
                Kind = Microsoft365SourceKind.SharePointList,
                ExternalResourceId = discoveredList.MicrosoftResourceId,
                ParentExternalResourceId = site.SiteId,
                DisplayName = discoveredList.DisplayName,
                WebUrl = discoveredList.WebUrl,
                Status = Microsoft365SourceStatus.Discovered,
                IsIndexed = false,
                DiscoveredAt = discoveredAt
            });
        }

        foreach (var missingList in existingLists.Where(list => !discoveredIds.Contains(list.ListId)))
        {
            missingList.MarkUnavailable();
        }
    }

    private static IEnumerable<Microsoft365SourceDiscoveryData> DistinctByMicrosoftId(
        IEnumerable<Microsoft365SourceDiscoveryData> sources) =>
        sources
            .GroupBy(source => source.MicrosoftResourceId, StringComparer.Ordinal)
            .Select(group => group.Last());
}
