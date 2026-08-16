using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class OrganizationConnectorPersistenceConfigurationTests
{
    [Theory, AutoDomainData]
    public void Given_ConnectorModels_When_InspectingConfiguration_Then_ConstraintsAndRelationsAreConfigured(
        Guid databaseId)
    {
        // Given
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(databaseId.ToString())
            .Options;
        using var dbContext = new AssistantCoreDbContext(options);

        // When
        var connectorType = dbContext.Model.FindEntityType(typeof(OrganizationConnector));
        var sourceType = dbContext.Model.FindEntityType(typeof(OrganizationConnectorSource));

        // Then
        Assert.NotNull(connectorType);
        Assert.Equal(50, connectorType.FindProperty(nameof(OrganizationConnector.Type))?.GetMaxLength());
        Assert.Equal(20, connectorType.FindProperty(nameof(OrganizationConnector.Status))?.GetMaxLength());
        Assert.Contains(
            connectorType.GetIndexes(),
            index => index.IsUnique
                && index.Properties.Select(property => property.Name).SequenceEqual(
                    [nameof(OrganizationConnector.OrganizationId), nameof(OrganizationConnector.Type)]));
        Assert.Contains(
            connectorType.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Organization)
                && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);

        Assert.NotNull(sourceType);
        Assert.Equal(50, sourceType.FindProperty(nameof(OrganizationConnectorSource.SourceType))?.GetMaxLength());
        Assert.Equal(20, sourceType.FindProperty(nameof(OrganizationConnectorSource.Status))?.GetMaxLength());
        Assert.Equal(
            [nameof(OrganizationConnectorSource.OrganizationConnectorId), nameof(OrganizationConnectorSource.SourceType)],
            sourceType.FindPrimaryKey()?.Properties.Select(property => property.Name));
        Assert.Contains(
            sourceType.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(OrganizationConnector)
                && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
    }
}
