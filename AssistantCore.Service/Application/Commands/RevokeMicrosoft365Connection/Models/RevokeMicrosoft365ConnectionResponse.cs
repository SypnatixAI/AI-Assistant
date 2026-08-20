namespace AssistantCore.Service.Application.Commands.RevokeMicrosoft365Connection.Models;

public sealed record RevokeMicrosoft365ConnectionResponse(
    Guid ConnectionId,
    string Status);
