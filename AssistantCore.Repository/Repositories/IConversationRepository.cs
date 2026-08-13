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
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default);
}
