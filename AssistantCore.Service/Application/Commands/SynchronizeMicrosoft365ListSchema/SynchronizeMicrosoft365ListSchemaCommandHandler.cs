using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.SynchronizeMicrosoft365ListSchema;

public sealed class SynchronizeMicrosoft365ListSchemaCommandHandler(
    IMicrosoft365ListSynchronizationService synchronizationService)
    : IRequestHandler<SynchronizeMicrosoft365ListSchemaCommand, Microsoft365ListSchemaSynchronizationResult>
{
    public Task<Microsoft365ListSchemaSynchronizationResult> HandleAsync(
        SynchronizeMicrosoft365ListSchemaCommand request,
        CancellationToken cancellationToken) =>
        synchronizationService.SynchronizeSchemaAsync(request.SourceId, cancellationToken);
}
