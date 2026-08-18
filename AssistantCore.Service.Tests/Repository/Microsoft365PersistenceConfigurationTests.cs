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

        Assert.NotNull(subscriptionType);
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
