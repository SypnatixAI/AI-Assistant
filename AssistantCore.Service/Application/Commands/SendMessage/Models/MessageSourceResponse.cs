namespace AssistantCore.Service.Application.Commands.SendMessage.Models;

public sealed record MessageSourceResponse(
    string Type,
    string Title,
    string? Url,
    string Reference);
