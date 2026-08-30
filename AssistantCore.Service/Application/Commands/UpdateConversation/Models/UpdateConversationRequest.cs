namespace AssistantCore.Service.Application.Commands.UpdateConversation.Models;

public sealed record UpdateConversationRequest(
    string? Title,
    string? Status);
