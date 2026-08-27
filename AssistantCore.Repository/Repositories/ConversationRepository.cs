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

    public async Task<ConversationListPage> ListConversationsAsync(
        Guid organizationId,
        Guid ownerMemberId,
        int limit,
        DateTimeOffset? cursorUpdatedAt,
        Guid? cursorId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Conversations
            .AsNoTracking()
            .Where(conversation =>
                conversation.OrganizationId == organizationId
                && conversation.OwnerMemberId == ownerMemberId
                && conversation.Status == ConversationStatus.Active);

        if (cursorUpdatedAt is not null && cursorId is not null)
        {
            query = query.Where(conversation =>
                conversation.UpdatedAt < cursorUpdatedAt
                || (conversation.UpdatedAt == cursorUpdatedAt && conversation.Id < cursorId));
        }

        var items = await query
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ThenByDescending(conversation => conversation.Id)
            .Take(limit + 1)
            .Select(conversation => new ConversationListItem(
                conversation.Id,
                conversation.Title,
                conversation.CreatedAt,
                conversation.UpdatedAt,
                conversation.Messages
                    .OrderByDescending(message => message.CreatedAt)
                    .ThenByDescending(message => message.Id)
                    .Select(message => message.Content)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;

        return new ConversationListPage(
            hasMore ? items.Take(limit).ToList() : items,
            hasMore);
    }

    public async Task<Message?> AddUserMessageAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        Message userMessage,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(
            userMessage.ConversationId,
            conversationId,
            nameof(userMessage.ConversationId));

        var conversationExists = await dbContext.Conversations
            .AnyAsync(
                conversation =>
                    conversation.Id == conversationId
                    && conversation.OrganizationId == organizationId
                    && conversation.OwnerMemberId == ownerMemberId,
                cancellationToken);

        if (!conversationExists)
        {
            return null;
        }

        userMessage.ConversationId = conversationId;
        userMessage.Role = MessageRole.User;
        userMessage.ProcessingStatus = MessageProcessingStatus.Pending;

        dbContext.Messages.Add(userMessage);
        await dbContext.SaveChangesAsync(cancellationToken);

        return userMessage;
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
        IReadOnlyCollection<MessageWarning> warnings,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        var userMessage = await dbContext.Messages
            .Include(message => message.Conversation)
            .SingleOrDefaultAsync(
                message =>
                    message.Id == userMessageId
                    && message.Role == MessageRole.User
                    && message.ProcessingStatus == MessageProcessingStatus.InProgress
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

        foreach (var warning in warnings)
        {
            warning.MessageId = assistantMessage.Id;
            assistantMessage.Warnings.Add(warning);
        }

        userMessage.ProcessingStatus = MessageProcessingStatus.Completed;
        userMessage.UpdatedAt = completedAt;
        userMessage.Conversation.UpdatedAt = completedAt;
        dbContext.Messages.Add(assistantMessage);

        await dbContext.SaveChangesAsync(cancellationToken);

        return assistantMessage;
    }

    public async Task<bool> FailMessageProcessingAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        Guid userMessageId,
        MessageProcessingStatus failureStatus,
        string errorCode,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken = default)
    {
        if (failureStatus is not MessageProcessingStatus.Failed
            and not MessageProcessingStatus.Cancelled)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureStatus),
                failureStatus,
                "The failure status must be Failed or Cancelled.");
        }

        var userMessage = await dbContext.Messages
            .SingleOrDefaultAsync(
                message =>
                    message.Id == userMessageId
                    && message.Role == MessageRole.User
                    && message.ProcessingStatus == MessageProcessingStatus.InProgress
                    && message.ConversationId == conversationId
                    && message.Conversation.OrganizationId == organizationId
                    && message.Conversation.OwnerMemberId == ownerMemberId,
                cancellationToken);

        if (userMessage is null)
        {
            return false;
        }

        userMessage.ProcessingStatus = failureStatus;
        userMessage.ProcessingErrorCode = errorCode;
        userMessage.UpdatedAt = failedAt;

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
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
