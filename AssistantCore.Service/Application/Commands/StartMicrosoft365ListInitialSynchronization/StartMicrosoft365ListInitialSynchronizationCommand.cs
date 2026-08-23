using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.StartMicrosoft365ListInitialSynchronization;

public sealed record StartMicrosoft365ListInitialSynchronizationCommand(
    Guid SourceId,
    Guid SynchronizationId) : IRequest<Microsoft365ListInitialSynchronizationResult>;
