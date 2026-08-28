namespace AssistantCore.Service.Application.Models.Conversations;

public sealed record GetConversationMessagesResponse(
    Guid ConversationId,
    IReadOnlyCollection<ConversationMessageResponse> Messages,
    string? NextCursor,
    bool HasMore);
