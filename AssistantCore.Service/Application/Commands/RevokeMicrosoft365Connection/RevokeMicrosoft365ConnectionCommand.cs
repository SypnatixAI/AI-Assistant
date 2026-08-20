using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.RevokeMicrosoft365Connection.Models;

namespace AssistantCore.Service.Application.Commands.RevokeMicrosoft365Connection;

public sealed record RevokeMicrosoft365ConnectionCommand(Guid ConnectionId)
    : IRequest<RevokeMicrosoft365ConnectionResponse>;
