using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

/// <summary>
/// Isolation Microsoft 365 entre organisations lorsque les deux possedent
/// exactement les memes identifiants externes. Les identifiants SharePoint sont
/// choisis par Microsoft, pas par AssistantCore : deux organisations peuvent donc
/// heberger un site, une liste ou un lecteur portant le meme identifiant.
/// Un test qui oppose une organisation reelle a un identifiant inexistant ne
/// prouve rien ici; il faut que la donnee concurrente existe reellement.
/// </summary>
public sealed class Microsoft365CrossOrganizationIsolationTests
{
    private const string SharedSiteId = "shared-site-id";
    private const string SharedListId = "shared-list-id";
    private const string SharedDriveId = "shared-drive-id";

    [Theory, AutoDomainData]
    public async Task Given_TheSameSiteIdInTwoOrganizations_When_FindSiteAsync_Then_EachOneSeesOnlyItsOwn(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        var beta = await SeedOrganizationAsync(dbContext, "Beta");
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);

        // When
        var alphaSite = await repository.FindSiteAsync(
            alpha.OrganizationId,
            SharedSiteId,
            CancellationToken.None);
        var betaSite = await repository.FindSiteAsync(
            beta.OrganizationId,
            SharedSiteId,
            CancellationToken.None);

        // Then
        Assert.NotNull(alphaSite);
        Assert.NotNull(betaSite);
        Assert.NotEqual(alphaSite.Id, betaSite.Id);
        Assert.Equal(alpha.Site.Id, alphaSite.Id);
        Assert.Equal(beta.Site.Id, betaSite.Id);
        Assert.Equal("Site Alpha", alphaSite.DisplayName);
        Assert.Equal("Site Beta", betaSite.DisplayName);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheSameListIdInTwoOrganizations_When_GetListsAsync_Then_EachOneSeesOnlyItsOwn(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        var beta = await SeedOrganizationAsync(dbContext, "Beta");
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);

        // When
        var alphaLists = await repository.GetListsAsync(
            alpha.OrganizationId,
            SharedSiteId,
            CancellationToken.None);
        var betaLists = await repository.GetListsAsync(
            beta.OrganizationId,
            SharedSiteId,
            CancellationToken.None);

        // Then
        Assert.Equal(alpha.List.Id, Assert.Single(alphaLists).Id);
        Assert.Equal(beta.List.Id, Assert.Single(betaLists).Id);
        Assert.Equal("Liste Alpha", alphaLists.Single().DisplayName);
        Assert.Equal("Liste Beta", betaLists.Single().DisplayName);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheSameDriveIdInTwoOrganizations_When_FindDriveAsync_Then_EachOneSeesOnlyItsOwn(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        var beta = await SeedOrganizationAsync(dbContext, "Beta");
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);

        // When
        var alphaDrive = await repository.FindDriveAsync(
            alpha.OrganizationId,
            SharedSiteId,
            SharedDriveId,
            CancellationToken.None);
        var betaDrive = await repository.FindDriveAsync(
            beta.OrganizationId,
            SharedSiteId,
            SharedDriveId,
            CancellationToken.None);

        // Then
        Assert.NotNull(alphaDrive);
        Assert.NotNull(betaDrive);
        Assert.Equal(alpha.Drive.Id, alphaDrive.Id);
        Assert.Equal(beta.Drive.Id, betaDrive.Id);
        Assert.NotEqual(alphaDrive.Id, betaDrive.Id);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheSameSiteIdInTwoOrganizations_When_GetSiteIdsAsync_Then_NeitherListLeaksIntoTheOther(
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        var beta = await SeedOrganizationAsync(dbContext, "Beta");
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);

        // When
        var alphaSiteIds = await repository.GetSiteIdsAsync(
            alpha.OrganizationId,
            CancellationToken.None);
        var betaSiteIds = await repository.GetSiteIdsAsync(
            beta.OrganizationId,
            CancellationToken.None);

        // Then
        Assert.Equal(SharedSiteId, Assert.Single(alphaSiteIds));
        Assert.Equal(SharedSiteId, Assert.Single(betaSiteIds));
    }

    [Theory, AutoDomainData]
    public async Task Given_TheSameSiteIdInTwoOrganizations_When_SaveSiteAsync_Then_UpdatesOnlyTheCallerOrganization(
        Guid databaseId,
        DateTimeOffset discoveredAt)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var alpha = await SeedOrganizationAsync(dbContext, "Alpha");
        var beta = await SeedOrganizationAsync(dbContext, "Beta");
        var repository = new Microsoft365SourceDiscoveryRepository(dbContext);

        // When
        var saved = await repository.SaveSiteAsync(
            alpha.Connection,
            SharedSiteId,
            "Site Alpha renomme",
            "https://alpha.local/sites/partage",
            discoveredAt,
            CancellationToken.None);

        // Then
        Assert.Equal(alpha.Site.Id, saved.Id);
        Assert.Equal(alpha.OrganizationId, saved.OrganizationId);

        var betaSite = await dbContext.Microsoft365Sites
            .SingleAsync(site => site.Id == beta.Site.Id);
        Assert.Equal("Site Beta", betaSite.DisplayName);
        Assert.Equal(2, await dbContext.Microsoft365Sites.CountAsync());
    }

    private sealed record SeededOrganization(
        Guid OrganizationId,
        Microsoft365Connection Connection,
        Microsoft365Site Site,
        Microsoft365List List,
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
            SiteId = SharedSiteId,
            Kind = Microsoft365SourceKind.SharePointSite,
            ExternalResourceId = SharedSiteId,
            DisplayName = $"Site {name}",
            Status = Microsoft365SourceStatus.Enabled,
            IsIndexed = false,
            DiscoveredAt = now
        };
        var list = new Microsoft365List
        {
            Id = Guid.NewGuid(),
            Microsoft365ConnectionId = connectionId,
            OrganizationId = organizationId,
            OrganizationConnectorId = connectorId,
            SiteId = SharedSiteId,
            ListId = SharedListId,
            Kind = Microsoft365SourceKind.SharePointList,
            ExternalResourceId = SharedListId,
            ParentExternalResourceId = SharedSiteId,
            DisplayName = $"Liste {name}",
            Status = Microsoft365SourceStatus.Discovered,
            IsIndexed = false,
            DiscoveredAt = now
        };
        var drive = new Microsoft365Drive
        {
            Id = Guid.NewGuid(),
            Microsoft365ConnectionId = connectionId,
            OrganizationId = organizationId,
            OrganizationConnectorId = connectorId,
            SiteId = SharedSiteId,
            DriveId = SharedDriveId,
            Kind = Microsoft365SourceKind.SharePointDrive,
            ExternalResourceId = SharedDriveId,
            ParentExternalResourceId = SharedSiteId,
            DisplayName = $"Lecteur {name}",
            Status = Microsoft365SourceStatus.Discovered,
            IsIndexed = false,
            DiscoveredAt = now
        };

        dbContext.AddRange(organization, connector, connection, site, list, drive);
        await dbContext.SaveChangesAsync();

        return new SeededOrganization(organizationId, connection, site, list, drive);
    }

    private static AssistantCoreDbContext CreateDbContext(Guid databaseId)
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(databaseId.ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
