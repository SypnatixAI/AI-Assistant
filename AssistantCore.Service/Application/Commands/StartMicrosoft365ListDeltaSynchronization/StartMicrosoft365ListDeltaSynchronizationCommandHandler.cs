using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.StartMicrosoft365ListDeltaSynchronization;

public sealed class StartMicrosoft365ListDeltaSynchronizationCommandHandler(
    IMicrosoft365ListSynchronizationService synchronizationService)
    : IRequestHandler<StartMicrosoft365ListDeltaSynchronizationCommand, Microsoft365ListDeltaSynchronizationResult>
{
    public Task<Microsoft365ListDeltaSynchronizationResult> HandleAsync(
        StartMicrosoft365ListDeltaSynchronizationCommand request,
        CancellationToken cancellationToken) =>
        synchronizationService.StartDeltaSynchronizationAsync(
            request.SourceId,
            request.SynchronizationId,
            cancellationToken);
}
