using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryCompleteMessageTests
{
    [Theory, AutoDomainData]
    public async Task Given_AValidatedAssistantResponse_When_CompleteMessageWithAssistantResponseAsync_Then_PersistsResponseSourcesAndCompletion(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message userMessage,
        Message assistantMessage,
        MessageSource source,
        MessageWarning warning,
        DateTimeOffset completedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        userMessage.ConversationId = conversation.Id;
        userMessage.Role = MessageRole.User;
        userMessage.ProcessingStatus = MessageProcessingStatus.InProgress;
        assistantMessage.CreatedAt = userMessage.CreatedAt.AddSeconds(1);
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(userMessage);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.CompleteMessageWithAssistantResponseAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            userMessage.Id,
            assistantMessage,
            [source],
            [warning],
            completedAt,
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedConversation = await dbContext.Conversations.SingleAsync();
        var persistedMessages = await dbContext.Messages
            .OrderBy(message => message.CreatedAt)
            .ToArrayAsync();
        var persistedSource = await dbContext.MessageSources.SingleAsync();
        var persistedWarning = await dbContext.MessageWarnings.SingleAsync();
        Assert.Same(assistantMessage, result);
        Assert.Equal(MessageProcessingStatus.Completed, persistedMessages[0].ProcessingStatus);
        Assert.Equal(MessageRole.Assistant, persistedMessages[1].Role);
        Assert.Equal(conversation.Id, persistedMessages[1].ConversationId);
        Assert.Equal(assistantMessage.Id, persistedSource.MessageId);
        Assert.Equal(assistantMessage.Id, persistedWarning.MessageId);
        Assert.Equal(warning.Content, persistedWarning.Content);
        Assert.Equal(completedAt, persistedConversation.UpdatedAt);
    }

    [Theory, AutoDomainData]
    public async Task Given_AWrongOwner_When_CompleteMessageWithAssistantResponseAsync_Then_DoesNotPersistAssistantResponse(
        Guid organizationId,
        Guid ownerMemberId,
        Guid wrongOwnerMemberId,
        Conversation conversation,
        Message userMessage,
        Message assistantMessage,
        MessageSource source,
        MessageWarning warning,
        DateTimeOffset completedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        userMessage.ConversationId = conversation.Id;
        userMessage.Role = MessageRole.User;
        userMessage.ProcessingStatus = MessageProcessingStatus.InProgress;
        var originalProcessingStatus = userMessage.ProcessingStatus;
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(userMessage);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.CompleteMessageWithAssistantResponseAsync(
            conversation.OrganizationId,
            wrongOwnerMemberId,
            conversation.Id,
            userMessage.Id,
            assistantMessage,
            [source],
            [warning],
            completedAt,
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedUserMessage = await dbContext.Messages.SingleAsync();
        Assert.Null(result);
        Assert.Equal(originalProcessingStatus, persistedUserMessage.ProcessingStatus);
        Assert.Empty(dbContext.MessageSources);
        Assert.Empty(dbContext.MessageWarnings);
    }

    [Theory]
    [InlineAutoDomainData(MessageProcessingStatus.Pending)]
    [InlineAutoDomainData(MessageProcessingStatus.Completed)]
    [InlineAutoDomainData(MessageProcessingStatus.Failed)]
    [InlineAutoDomainData(MessageProcessingStatus.Cancelled)]
    public async Task Given_AUserMessageNotInProgress_When_CompleteMessageWithAssistantResponseAsync_Then_DoesNotPersistCompletion(
        MessageProcessingStatus processingStatus,
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message userMessage,
        Message assistantMessage,
        MessageSource source,
        MessageWarning warning,
        DateTimeOffset completedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        userMessage.ConversationId = conversation.Id;
        userMessage.Role = MessageRole.User;
        userMessage.ProcessingStatus = processingStatus;
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(userMessage);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.CompleteMessageWithAssistantResponseAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            userMessage.Id,
            assistantMessage,
            [source],
            [warning],
            completedAt,
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedUserMessage = await dbContext.Messages.SingleAsync();
        Assert.Null(result);
        Assert.Equal(processingStatus, persistedUserMessage.ProcessingStatus);
        Assert.Empty(dbContext.MessageSources);
        Assert.Empty(dbContext.MessageWarnings);
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
