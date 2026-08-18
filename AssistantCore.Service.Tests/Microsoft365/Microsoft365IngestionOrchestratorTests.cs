using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365IngestionOrchestratorTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnActiveConnection_When_ScheduleInitialSynchronizationAsync_Then_ConnectionIsAccepted(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        // Given
        await using var dbContext = CreateDbContext();
        dbContext.Microsoft365Connections.Add(new Microsoft365Connection
        {
            Id = connectionId,
            Status = Microsoft365ConnectionStatus.Active
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        var orchestrator = new Microsoft365IngestionOrchestrator(
            new Microsoft365ConnectionRepository(dbContext));

        // When
        await orchestrator.ScheduleInitialSynchronizationAsync(connectionId, cancellationToken);

        // Then
        var storedConnection = await dbContext.Microsoft365Connections
            .SingleAsync(connection => connection.Id == connectionId, cancellationToken);
        Assert.Equal(Microsoft365ConnectionStatus.Active, storedConnection.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_ARevokedConnection_When_ScheduleInitialSynchronizationAsync_Then_ConnectionIsRejected(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        // Given
        await using var dbContext = CreateDbContext();
        dbContext.Microsoft365Connections.Add(new Microsoft365Connection
        {
            Id = connectionId,
            Status = Microsoft365ConnectionStatus.Revoked
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        var orchestrator = new Microsoft365IngestionOrchestrator(
            new Microsoft365ConnectionRepository(dbContext));

        // When
        var action = () => orchestrator.ScheduleInitialSynchronizationAsync(
            connectionId,
            cancellationToken);

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AssistantCoreDbContext(options);
    }
}
