namespace AssistantCore.Service.Application.Commands.SendMessage.Models;

public sealed record SendMessageRequest(
    Guid? ConversationId,
    string Message,
    string? Model);
