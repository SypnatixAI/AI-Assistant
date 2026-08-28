namespace AssistantCore.Service.Application.Services.Conversations.Pagination;

/// <summary>
/// Position decodee dans l'historique des messages d'une conversation, selon
/// l'ordre stable impose par la documentation : CreatedAt puis Id pour
/// departager deux messages crees a la meme date. Porte l'identifiant de la
/// conversation afin de detecter un curseur reutilise sur une autre
/// conversation.
/// </summary>
public sealed record ConversationMessageCursor(
    Guid ConversationId,
    DateTimeOffset CreatedAt,
    Guid Id);
