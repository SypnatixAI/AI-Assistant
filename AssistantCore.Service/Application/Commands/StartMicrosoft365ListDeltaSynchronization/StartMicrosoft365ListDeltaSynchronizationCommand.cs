using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.StartMicrosoft365ListDeltaSynchronization;

public sealed record StartMicrosoft365ListDeltaSynchronizationCommand(
    Guid SourceId,
    Guid SynchronizationId) : IRequest<Microsoft365ListDeltaSynchronizationResult>;
