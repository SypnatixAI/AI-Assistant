using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365CurrentUserOneDriveIndexingServiceTests
{
    [Fact]
    public async Task Given_TheAuthenticatedUserHasOneDrive_When_EnsuringIndexing_Then_TheirDriveIsAutomaticallyActivated()
    {
        var organizationId = Guid.NewGuid();
        const string tenantId = "tenant-1";
        const string entraUserId = "user-1";
        await using var dbContext = CreateDbContext();
        await SeedActiveConnectionAsync(dbContext, organizationId, tenantId);
        var client = new StubCurrentUserOneDriveClient(
            new Microsoft365CurrentUserDrive(
                "drive-1",
                "User OneDrive",
                "https://contoso-my.sharepoint.com/personal/user"));
        var service = CreateService(dbContext, client);

        await service.EnsureIndexedAsync(organizationId, entraUserId);

        var drive = await dbContext.Microsoft365Drives
            .Include(candidate => candidate.Synchronizations)
            .Include(candidate => candidate.Subscriptions)
            .SingleAsync();
        Assert.Equal(Microsoft365SourceKind.OneDrive, drive.Kind);
        Assert.Null(drive.SiteId);
        Assert.Equal(entraUserId, drive.OwnerUserObjectId);
        Assert.True(drive.IsIndexed);
        Assert.Equal(Microsoft365SourceStatus.Enabled, drive.Status);
        Assert.Contains(drive.Synchronizations, synchronization =>
            synchronization.Type == Microsoft365SynchronizationType.Initial
            && synchronization.Status == Microsoft365SynchronizationStatus.Pending);
        Assert.Contains(drive.Subscriptions, subscription =>
            subscription.Status == Microsoft365SubscriptionStatus.Pending
            && subscription.Resource == "/drives/drive-1/root");
        Assert.Equal(tenantId, client.LastTenantId);
        Assert.Equal(entraUserId, client.LastEntraUserId);
    }

    [Fact]
    public async Task Given_TheAuthenticatedUserHasNoOneDrive_When_EnsuringIndexing_Then_NothingIsPersisted()
    {
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        await SeedActiveConnectionAsync(dbContext, organizationId, "tenant-1");
        var client = new StubCurrentUserOneDriveClient(null);
        var service = CreateService(dbContext, client);

        await service.EnsureIndexedAsync(organizationId, "user-without-onedrive");

        Assert.Empty(await dbContext.Microsoft365Drives.ToArrayAsync());
    }

    [Fact]
    public async Task Given_TheOrganizationHasNoActiveMicrosoft365Connection_When_EnsuringIndexing_Then_GraphIsNotCalled()
    {
        await using var dbContext = CreateDbContext();
        var client = new StubCurrentUserOneDriveClient(
            new Microsoft365CurrentUserDrive("drive-1", "OneDrive", null));
        var service = CreateService(dbContext, client);

        await service.EnsureIndexedAsync(Guid.NewGuid(), "user-1");

        Assert.Null(client.LastTenantId);
        Assert.Empty(await dbContext.Microsoft365Drives.ToArrayAsync());
    }

    private static Microsoft365CurrentUserOneDriveIndexingService CreateService(
        AssistantCoreDbContext dbContext,
        IMicrosoft365CurrentUserOneDriveClient client) =>
        new(
            new Microsoft365ConnectionRepository(dbContext),
            new Microsoft365DriveRepository(dbContext),
            new Microsoft365SourceDiscoveryRepository(dbContext),
            client,
            TimeProvider.System);

    private static async Task SeedActiveConnectionAsync(
        AssistantCoreDbContext dbContext,
        Guid organizationId,
        string tenantId)
    {
        var connectorId = Guid.NewGuid();
        dbContext.OrganizationConnectors.Add(new OrganizationConnector
        {
            Id = connectorId,
            OrganizationId = organizationId,
            Type = ConnectorType.Microsoft365,
            Status = RecordStatus.Active,
            IsConfigured = true
        });
        dbContext.Microsoft365Connections.Add(new Microsoft365Connection
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OrganizationConnectorId = connectorId,
            TenantId = tenantId,
            Status = Microsoft365ConnectionStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AssistantCoreDbContext(options);
    }

    private sealed class StubCurrentUserOneDriveClient(Microsoft365CurrentUserDrive? drive)
        : IMicrosoft365CurrentUserOneDriveClient
    {
        public string? LastTenantId { get; private set; }
        public string? LastEntraUserId { get; private set; }

        public Task<Microsoft365CurrentUserDrive?> GetAsync(
            string tenantId,
            string entraUserId,
            CancellationToken cancellationToken = default)
        {
            LastTenantId = tenantId;
            LastEntraUserId = entraUserId;
            return Task.FromResult(drive);
        }
    }
}
