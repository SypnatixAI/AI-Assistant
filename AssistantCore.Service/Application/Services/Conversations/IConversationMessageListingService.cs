namespace AssistantCore.Service.Application.Services.Conversations;

public interface IConversationMessageListingService
{
    /// <summary>
    /// Retourne la page de messages demandee, ou null lorsque la conversation
    /// n'existe pas ou n'appartient pas au membre/organisation fournis.
    /// </summary>
    Task<ConversationMessageListingPage?> ListAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        int limit,
        DateTimeOffset? cursorCreatedAt,
        Guid? cursorId,
        CancellationToken cancellationToken);
}
