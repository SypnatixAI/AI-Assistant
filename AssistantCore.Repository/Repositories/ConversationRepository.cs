using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Repositories;

public sealed class ConversationRepository(AssistantCoreDbContext dbContext)
    : IConversationRepository
{
    public async Task<(Conversation Conversation, Message UserMessage)> CreateConversationWithFirstMessageAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message userMessage,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(
            conversation.OrganizationId,
            organizationId,
            nameof(conversation.OrganizationId));
        ValidateIdentifier(
            conversation.OwnerMemberId,
            ownerMemberId,
            nameof(conversation.OwnerMemberId));
        ValidateIdentifier(
            userMessage.ConversationId,
            conversation.Id,
            nameof(userMessage.ConversationId));

        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        userMessage.ConversationId = conversation.Id;
        userMessage.Role = MessageRole.User;
        userMessage.ProcessingStatus = MessageProcessingStatus.Pending;

        conversation.Messages.Add(userMessage);
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (conversation, userMessage);
    }

    public Task<Conversation?> FindConversationAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                conversation =>
                    conversation.Id == conversationId
                    && conversation.OrganizationId == organizationId
                    && conversation.OwnerMemberId == ownerMemberId,
                cancellationToken);
    }

    public async Task<bool> UpdateMessageProcessingStatusAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        Guid messageId,
        MessageProcessingStatus status,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
    {
        var message = await dbContext.Messages
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == messageId
                    && candidate.Role == MessageRole.User
                    && candidate.ConversationId == conversationId
                    && candidate.Conversation.OrganizationId == organizationId
                    && candidate.Conversation.OwnerMemberId == ownerMemberId,
                cancellationToken);

        if (message is null)
        {
            return false;
        }

        message.ProcessingStatus = status;
        message.UpdatedAt = updatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<Message?> CompleteMessageWithAssistantResponseAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        Guid userMessageId,
        Message assistantMessage,
        IReadOnlyCollection<MessageSource> sources,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        var userMessage = await dbContext.Messages
            .Include(message => message.Conversation)
            .SingleOrDefaultAsync(
                message =>
                    message.Id == userMessageId
                    && message.Role == MessageRole.User
                    && message.ConversationId == conversationId
                    && message.Conversation.OrganizationId == organizationId
                    && message.Conversation.OwnerMemberId == ownerMemberId,
                cancellationToken);

        if (userMessage is null)
        {
            return null;
        }

        assistantMessage.ConversationId = conversationId;
        assistantMessage.Role = MessageRole.Assistant;
        assistantMessage.ProcessingStatus = MessageProcessingStatus.Completed;

        foreach (var source in sources)
        {
            source.MessageId = assistantMessage.Id;
            assistantMessage.Sources.Add(source);
        }

        userMessage.ProcessingStatus = MessageProcessingStatus.Completed;
        userMessage.UpdatedAt = completedAt;
        userMessage.Conversation.UpdatedAt = completedAt;
        dbContext.Messages.Add(assistantMessage);

        await dbContext.SaveChangesAsync(cancellationToken);

        return assistantMessage;
    }

    private static void ValidateIdentifier(
        Guid currentValue,
        Guid expectedValue,
        string parameterName)
    {
        if (currentValue != Guid.Empty && currentValue != expectedValue)
        {
            throw new ArgumentException(
                $"{parameterName} does not match the authenticated context.",
                parameterName);
        }
    }
}
