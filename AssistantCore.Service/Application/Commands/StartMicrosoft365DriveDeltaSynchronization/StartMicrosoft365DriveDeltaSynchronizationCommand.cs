using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.StartMicrosoft365DriveDeltaSynchronization;

public sealed record StartMicrosoft365DriveDeltaSynchronizationCommand(
    Guid SourceId,
    Guid SynchronizationId) : IRequest<Microsoft365DriveDeltaSynchronizationResult>;
