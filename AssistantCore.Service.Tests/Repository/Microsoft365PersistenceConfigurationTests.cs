using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class Microsoft365PersistenceConfigurationTests
{
    [Theory, AutoDomainData]
    public void Given_Microsoft365Models_When_InspectingConfiguration_Then_UniqueTenantAndSourceConstraintsAreConfigured(
        Guid databaseId)
    {
        // Given
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(databaseId.ToString())
            .Options;
        using var dbContext = new AssistantCoreDbContext(options);

        // When
        var connectionType = dbContext.Model.FindEntityType(typeof(Microsoft365Connection));
        var sourceType = dbContext.Model.FindEntityType(typeof(Microsoft365Source));
        var siteType = dbContext.Model.FindEntityType(typeof(Microsoft365Site));
        var driveType = dbContext.Model.FindEntityType(typeof(Microsoft365Drive));
        var listType = dbContext.Model.FindEntityType(typeof(Microsoft365List));
        var subscriptionType = dbContext.Model.FindEntityType(typeof(Microsoft365Subscription));
        var synchronizationType = dbContext.Model.FindEntityType(typeof(Microsoft365Synchronization));

        // Then
        Assert.NotNull(connectionType);
        Assert.Contains(connectionType.GetIndexes(), index =>
            index.IsUnique && HasProperties(index, nameof(Microsoft365Connection.OrganizationId)));
        Assert.Contains(connectionType.GetIndexes(), index =>
            index.IsUnique && HasProperties(index, nameof(Microsoft365Connection.TenantId)));
        Assert.Contains(connectionType.GetIndexes(), index =>
            index.IsUnique && HasProperties(index, nameof(Microsoft365Connection.ConsentStateHash)));

        Assert.NotNull(sourceType);
        Assert.Contains(sourceType.GetIndexes(), index =>
            index.IsUnique && HasProperties(
                index,
                nameof(Microsoft365Source.Microsoft365ConnectionId),
                nameof(Microsoft365Source.Kind),
                nameof(Microsoft365Source.ExternalResourceId)));

        Assert.NotNull(siteType);
        Assert.Equal("Microsoft365Site", siteType.GetTableName());
        Assert.Contains(siteType.GetIndexes(), index =>
            index.IsUnique && HasProperties(
                index,
                nameof(Microsoft365Site.OrganizationId),
                nameof(Microsoft365Site.OrganizationConnectorId),
                nameof(Microsoft365Site.SiteId)));

        Assert.NotNull(driveType);
        Assert.Equal("Microsoft365Drive", driveType.GetTableName());
        Assert.Contains(driveType.GetIndexes(), index =>
            index.IsUnique && HasProperties(
                index,
                nameof(Microsoft365Drive.OrganizationId),
                nameof(Microsoft365Drive.OrganizationConnectorId),
                nameof(Microsoft365Drive.SiteId),
                nameof(Microsoft365Drive.DriveId)));

        Assert.NotNull(listType);
        Assert.Equal("Microsoft365List", listType.GetTableName());
        Assert.NotNull(listType.FindProperty(nameof(Microsoft365List.DisplayName)));
        Assert.NotNull(listType.FindProperty(nameof(Microsoft365List.WebUrl)));
        Assert.NotNull(listType.FindProperty(nameof(Microsoft365List.Status)));
        Assert.NotNull(listType.FindProperty(nameof(Microsoft365List.IsIndexed)));
        Assert.Contains(listType.GetIndexes(), index =>
            index.IsUnique && HasProperties(
                index,
                nameof(Microsoft365List.OrganizationId),
                nameof(Microsoft365List.OrganizationConnectorId),
                nameof(Microsoft365List.SiteId),
                nameof(Microsoft365List.ListId)));

        Assert.NotNull(subscriptionType);
        Assert.NotNull(subscriptionType.FindProperty(nameof(Microsoft365Subscription.OrganizationId)));
        Assert.NotNull(subscriptionType.FindProperty(nameof(Microsoft365Subscription.Resource)));
        Assert.Contains(subscriptionType.GetIndexes(), index =>
            index.IsUnique && HasProperties(index, nameof(Microsoft365Subscription.MicrosoftSubscriptionId)));

        Assert.NotNull(synchronizationType);
        Assert.Contains(synchronizationType.GetIndexes(), index =>
            index.IsUnique && HasProperties(index, nameof(Microsoft365Synchronization.Microsoft365SourceId)));
    }

    private static bool HasProperties(
        Microsoft.EntityFrameworkCore.Metadata.IReadOnlyIndex index,
        params string[] propertyNames) =>
        index.Properties.Select(property => property.Name).SequenceEqual(propertyNames);
}
