using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Queries;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class OrganizationConnectorQueriesTests
{
    [Theory, AutoDomainData]
    public async Task Given_MixedConnectorAvailability_When_GetActiveConfiguredConnectors_Then_ReturnsOnlyUsableOrganizationConnectors(
        Guid organizationId,
        Guid otherOrganizationId,
        Guid databaseId)
    {
        // Given
        var activeMicrosoft365 = CreateConnector(
            organizationId,
            ConnectorType.Microsoft365,
            RecordStatus.Active,
            isConfigured: true);
        activeMicrosoft365.Sources =
        [
            CreateSource(activeMicrosoft365.Id, Microsoft365SourceType.SharePoint, RecordStatus.Active, true),
            CreateSource(activeMicrosoft365.Id, Microsoft365SourceType.OneDrive, RecordStatus.Active, false)
        ];
        var activeErp = CreateConnector(
            organizationId,
            ConnectorType.Erp,
            RecordStatus.Active,
            isConfigured: true);
        var inactiveCrm = CreateConnector(
            organizationId,
            ConnectorType.Crm,
            RecordStatus.Inactive,
            isConfigured: true);
        var unconfiguredInternalData = CreateConnector(
            organizationId,
            ConnectorType.InternalData,
            RecordStatus.Active,
            isConfigured: false);
        var otherOrganizationConnector = CreateConnector(
            otherOrganizationId,
            ConnectorType.Crm,
            RecordStatus.Active,
            isConfigured: true);
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(databaseId.ToString())
            .Options;
        await using var dbContext = new AssistantCoreDbContext(options);
        dbContext.OrganizationConnectors.AddRange(
            activeMicrosoft365,
            activeErp,
            inactiveCrm,
            unconfiguredInternalData,
            otherOrganizationConnector);
        await dbContext.SaveChangesAsync();
        var queries = new OrganizationConnectorQueries(dbContext);

        // When
        var connectors = await queries.GetActiveConfiguredConnectors(
            organizationId,
            CancellationToken.None);

        // Then
        Assert.Equal(
            [ConnectorType.Microsoft365, ConnectorType.Erp],
            connectors.Select(connector => connector.Type));
        var source = Assert.Single(connectors.Single(
            connector => connector.Type == ConnectorType.Microsoft365).Sources);
        Assert.Equal(Microsoft365SourceType.SharePoint, source.SourceType);
    }

    private static OrganizationConnector CreateConnector(
        Guid organizationId,
        ConnectorType type,
        RecordStatus status,
        bool isConfigured) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Type = type,
            Status = status,
            IsConfigured = isConfigured
        };

    private static OrganizationConnectorSource CreateSource(
        Guid connectorId,
        Microsoft365SourceType sourceType,
        RecordStatus status,
        bool isIndexed) => new()
        {
            OrganizationConnectorId = connectorId,
            SourceType = sourceType,
            Status = status,
            IsIndexed = isIndexed
        };
}
