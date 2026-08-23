using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.StartMicrosoft365DriveInitialSynchronization;

public sealed record StartMicrosoft365DriveInitialSynchronizationCommand(
    Guid SourceId,
    Guid SynchronizationId) : IRequest<Microsoft365DriveInitialSynchronizationResult>;
