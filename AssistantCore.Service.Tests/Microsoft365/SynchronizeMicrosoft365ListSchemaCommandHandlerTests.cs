using AssistantCore.Service.Application.Commands.SynchronizeMicrosoft365ListSchema;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class SynchronizeMicrosoft365ListSchemaCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AListSource_When_HandleAsync_Then_DelegatesToSynchronizationService(
        Guid sourceId,
        Microsoft365ListSchemaSynchronizationResult expectedResult,
        CancellationToken cancellationToken)
    {
        // Given
        var service = new RecordingSynchronizationService(expectedResult);
        var handler = new SynchronizeMicrosoft365ListSchemaCommandHandler(service);
        var command = new SynchronizeMicrosoft365ListSchemaCommand(sourceId);

        // When
        var result = await handler.HandleAsync(command, cancellationToken);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(sourceId, service.SourceId);
        Assert.Equal(cancellationToken, service.CancellationToken);
    }

    private sealed class RecordingSynchronizationService(
        Microsoft365ListSchemaSynchronizationResult result)
        : IMicrosoft365ListSynchronizationService
    {
        public Guid? SourceId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<Microsoft365ListInitialSynchronizationResult> StartInitialSynchronizationAsync(
            Guid sourceId,
            Guid synchronizationId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Microsoft365ListDeltaSynchronizationResult> StartDeltaSynchronizationAsync(
            Guid sourceId,
            Guid synchronizationId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Microsoft365ListSchemaSynchronizationResult> SynchronizeSchemaAsync(
            Guid sourceId,
            CancellationToken cancellationToken = default)
        {
            SourceId = sourceId;
            CancellationToken = cancellationToken;
            return Task.FromResult(result);
        }
    }
}
