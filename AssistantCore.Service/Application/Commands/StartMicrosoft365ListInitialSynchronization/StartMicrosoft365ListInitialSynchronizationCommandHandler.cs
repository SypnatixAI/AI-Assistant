using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.StartMicrosoft365ListInitialSynchronization;

public sealed class StartMicrosoft365ListInitialSynchronizationCommandHandler(
    IMicrosoft365ListSynchronizationService synchronizationService)
    : IRequestHandler<StartMicrosoft365ListInitialSynchronizationCommand, Microsoft365ListInitialSynchronizationResult>
{
    public Task<Microsoft365ListInitialSynchronizationResult> HandleAsync(
        StartMicrosoft365ListInitialSynchronizationCommand request,
        CancellationToken cancellationToken) =>
        synchronizationService.StartInitialSynchronizationAsync(
            request.SourceId,
            request.SynchronizationId,
            cancellationToken);
}
