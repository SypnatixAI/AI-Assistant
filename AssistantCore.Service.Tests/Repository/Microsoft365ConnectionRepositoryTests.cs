using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class Microsoft365ConnectionRepositoryTests
{
    [Theory, AutoDomainData]
    public async Task Given_ACompletedConsent_When_CompleteConsentAsync_Then_ActivatesOneIndexedSharePointSource(
        Guid connectionId,
        Guid connectorId,
        Guid organizationId,
        string tenantId,
        DateTimeOffset completedAt,
        Guid databaseId)
    {
        // Given
        await using var dbContext = CreateDbContext(databaseId);
        var connector = new OrganizationConnector
        {
            Id = connectorId,
            OrganizationId = organizationId,
            Type = ConnectorType.Microsoft365,
            Status = RecordStatus.Inactive,
            IsConfigured = false
        };
        var connection = new Microsoft365Connection
        {
            Id = connectionId,
            OrganizationId = organizationId,
            OrganizationConnectorId = connectorId,
            OrganizationConnector = connector
        };
        dbContext.Microsoft365Connections.Add(connection);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var repository = new Microsoft365ConnectionRepository(dbContext);

        // When
        await repository.CompleteConsentAsync(
            connection,
            tenantId,
            completedAt,
            CancellationToken.None);
        var existingSource = await dbContext.OrganizationConnectorSources.SingleAsync();
        existingSource.Status = RecordStatus.Inactive;
        existingSource.IsIndexed = false;
        await dbContext.SaveChangesAsync(CancellationToken.None);
        connection.PrepareConsent(
            "replacement-state",
            completedAt.AddMinutes(10),
            completedAt);
        await repository.CompleteConsentAsync(
            connection,
            tenantId,
            completedAt,
            CancellationToken.None);

        // Then
        var source = Assert.Single(await dbContext.OrganizationConnectorSources.ToArrayAsync());
        Assert.Equal(connectorId, source.OrganizationConnectorId);
        Assert.Equal(Microsoft365SourceType.SharePoint, source.SourceType);
        Assert.Equal(RecordStatus.Active, source.Status);
        Assert.True(source.IsIndexed);
        Assert.Equal(RecordStatus.Active, connector.Status);
        Assert.True(connector.IsConfigured);
    }

    private static AssistantCoreDbContext CreateDbContext(Guid databaseId)
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(databaseId.ToString())
            .Options;
        return new AssistantCoreDbContext(options);
    }
}
