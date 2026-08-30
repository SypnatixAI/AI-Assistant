namespace AssistantCore.Service.Application.Commands.DeleteConversation.Models;

public sealed record DeleteConversationResponse(
    Guid ConversationId,
    bool AlreadyDeleted);
