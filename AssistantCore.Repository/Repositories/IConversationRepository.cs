using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Repositories;

public interface IConversationRepository
{
    Task<(Conversation Conversation, Message UserMessage)> CreateConversationWithFirstMessageAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message userMessage,
        CancellationToken cancellationToken = default);

    Task<Conversation?> FindConversationAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<ConversationMessagePage> ListMessagesAsync(
        Guid conversationId,
        int limit,
        DateTimeOffset? cursorCreatedAt,
        Guid? cursorId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationMessageItem>> GetConversationHistoryAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateConversationContextSummaryAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        string summary,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retourne une page de resumes de conversations visibles (statut Active),
    /// triees par UpdatedAt puis Id decroissants. Lit toujours limit + 1 elements
    /// pour determiner s'il existe une page suivante, sans jamais charger
    /// l'historique complet des messages.
    /// </summary>
    Task<ConversationListPage> ListConversationsAsync(
        Guid organizationId,
        Guid ownerMemberId,
        int limit,
        DateTimeOffset? cursorUpdatedAt,
        Guid? cursorId,
        CancellationToken cancellationToken = default);

    Task<Message?> AddUserMessageAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        Message userMessage,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateMessageProcessingStatusAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        Guid messageId,
        MessageProcessingStatus status,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default);

    Task<Message?> CompleteMessageWithAssistantResponseAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        Guid userMessageId,
        Message assistantMessage,
        IReadOnlyCollection<MessageSource> sources,
        IReadOnlyCollection<MessageWarning> warnings,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applique un renommage ou un changement de statut sur une conversation visible.
    /// Lorsque <paramref name="expectedVersion"/> est fourni, la mise a jour n'est appliquee
    /// que s'il correspond encore a la version persistee, ce qui protege contre une
    /// modification concurrente. Un appel sans version attendue ne verifie rien.
    /// </summary>
    Task<ConversationUpdateResult> UpdateConversationAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        int? expectedVersion,
        string? title,
        ConversationStatus? status,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marque une conversation comme supprimee et enregistre une demande de purge unique.
    /// L'operation est idempotente : repeter la suppression ne cree pas un second travail
    /// de purge et ne revele pas l'existence passee de la conversation.
    /// </summary>
    Task<ConversationDeleteStatus> SoftDeleteConversationAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        DateTimeOffset deletedAt,
        DateTimeOffset purgeAfter,
        CancellationToken cancellationToken = default);

    Task<bool> FailMessageProcessingAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        Guid userMessageId,
        MessageProcessingStatus failureStatus,
        string errorCode,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken = default);
}
