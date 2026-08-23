using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.StartMicrosoft365DriveDeltaSynchronization;

public sealed class StartMicrosoft365DriveDeltaSynchronizationCommandHandler(
    IMicrosoft365DriveSynchronizationService synchronizationService)
    : IRequestHandler<StartMicrosoft365DriveDeltaSynchronizationCommand, Microsoft365DriveDeltaSynchronizationResult>
{
    public Task<Microsoft365DriveDeltaSynchronizationResult> HandleAsync(
        StartMicrosoft365DriveDeltaSynchronizationCommand request,
        CancellationToken cancellationToken) =>
        synchronizationService.StartDeltaSynchronizationAsync(
            request.SourceId,
            request.SynchronizationId,
            cancellationToken);
}
