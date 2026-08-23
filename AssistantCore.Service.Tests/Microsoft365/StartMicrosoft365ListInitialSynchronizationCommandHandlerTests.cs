using AssistantCore.Service.Application.Commands.StartMicrosoft365ListInitialSynchronization;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class StartMicrosoft365ListInitialSynchronizationCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnInitialSynchronization_When_HandleAsync_Then_DelegatesToSynchronizationService(
        Guid sourceId,
        Guid synchronizationId,
        Microsoft365ListInitialSynchronizationResult expectedResult,
        CancellationToken cancellationToken)
    {
        // Given
        var service = new RecordingSynchronizationService(expectedResult);
        var handler = new StartMicrosoft365ListInitialSynchronizationCommandHandler(service);
        var command = new StartMicrosoft365ListInitialSynchronizationCommand(
            sourceId,
            synchronizationId);

        // When
        var result = await handler.HandleAsync(command, cancellationToken);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(sourceId, service.SourceId);
        Assert.Equal(synchronizationId, service.SynchronizationId);
        Assert.Equal(cancellationToken, service.CancellationToken);
    }

    private sealed class RecordingSynchronizationService(
        Microsoft365ListInitialSynchronizationResult result)
        : IMicrosoft365ListSynchronizationService
    {
        public Guid? SourceId { get; private set; }

        public Guid? SynchronizationId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<Microsoft365ListInitialSynchronizationResult> StartInitialSynchronizationAsync(
            Guid sourceId,
            Guid synchronizationId,
            CancellationToken cancellationToken = default)
        {
            SourceId = sourceId;
            SynchronizationId = synchronizationId;
            CancellationToken = cancellationToken;
            return Task.FromResult(result);
        }

        public Task<Microsoft365ListSchemaSynchronizationResult> SynchronizeSchemaAsync(
            Guid sourceId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Microsoft365ListDeltaSynchronizationResult> StartDeltaSynchronizationAsync(
            Guid sourceId,
            Guid synchronizationId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
