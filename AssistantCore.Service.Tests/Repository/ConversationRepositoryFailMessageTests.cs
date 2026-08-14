using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryFailMessageTests
{
    [Theory]
    [InlineAutoDomainData(MessageProcessingStatus.Failed)]
    [InlineAutoDomainData(MessageProcessingStatus.Cancelled)]
    public async Task Given_AnInProgressUserMessage_When_FailMessageProcessingAsync_Then_PersistsFailureWithoutAssistantMessage(
        MessageProcessingStatus failureStatus,
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message userMessage,
        DateTimeOffset failedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        userMessage.ConversationId = conversation.Id;
        userMessage.Role = MessageRole.User;
        userMessage.ProcessingStatus = MessageProcessingStatus.InProgress;
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(userMessage);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var updated = await repository.FailMessageProcessingAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            userMessage.Id,
            failureStatus,
            "provider_unavailable",
            failedAt,
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedMessage = await dbContext.Messages.SingleAsync();
        Assert.True(updated);
        Assert.Equal(failureStatus, persistedMessage.ProcessingStatus);
        Assert.Equal("provider_unavailable", persistedMessage.ProcessingErrorCode);
        Assert.Equal(failedAt, persistedMessage.UpdatedAt);
        Assert.Equal(MessageRole.User, persistedMessage.Role);
    }

    [Theory]
    [InlineAutoDomainData(MessageProcessingStatus.Completed)]
    [InlineAutoDomainData(MessageProcessingStatus.Failed)]
    [InlineAutoDomainData(MessageProcessingStatus.Cancelled)]
    public async Task Given_ATerminalUserMessage_When_FailMessageProcessingAsync_Then_DoesNotChangeItsState(
        MessageProcessingStatus initialStatus,
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message userMessage,
        DateTimeOffset failedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        userMessage.ConversationId = conversation.Id;
        userMessage.Role = MessageRole.User;
        userMessage.ProcessingStatus = initialStatus;
        userMessage.ProcessingErrorCode = null;
        var originalUpdatedAt = userMessage.UpdatedAt;
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(userMessage);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var updated = await repository.FailMessageProcessingAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            userMessage.Id,
            MessageProcessingStatus.Failed,
            "provider_unavailable",
            failedAt,
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedMessage = await dbContext.Messages.SingleAsync();
        Assert.False(updated);
        Assert.Equal(initialStatus, persistedMessage.ProcessingStatus);
        Assert.Null(persistedMessage.ProcessingErrorCode);
        Assert.Equal(originalUpdatedAt, persistedMessage.UpdatedAt);
    }

    [Theory, AutoDomainData]
    public async Task Given_AWrongOwner_When_FailMessageProcessingAsync_Then_DoesNotModifyTheMessage(
        Guid organizationId,
        Guid ownerMemberId,
        Guid wrongOwnerMemberId,
        Conversation conversation,
        Message userMessage,
        DateTimeOffset failedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        userMessage.ConversationId = conversation.Id;
        userMessage.Role = MessageRole.User;
        userMessage.ProcessingStatus = MessageProcessingStatus.InProgress;
        var originalUpdatedAt = userMessage.UpdatedAt;
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(userMessage);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var updated = await repository.FailMessageProcessingAsync(
            organizationId,
            wrongOwnerMemberId,
            conversation.Id,
            userMessage.Id,
            MessageProcessingStatus.Failed,
            "provider_unavailable",
            failedAt,
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedMessage = await dbContext.Messages.SingleAsync();
        Assert.False(updated);
        Assert.Equal(MessageProcessingStatus.InProgress, persistedMessage.ProcessingStatus);
        Assert.Null(persistedMessage.ProcessingErrorCode);
        Assert.Equal(originalUpdatedAt, persistedMessage.UpdatedAt);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInvalidFailureStatus_When_FailMessageProcessingAsync_Then_ThrowsBeforeReadingTheMessage(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        Guid userMessageId,
        DateTimeOffset failedAt)
    {
        // Given
        await using var dbContext = CreateDbContext();
        var repository = new ConversationRepository(dbContext);

        // When
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.FailMessageProcessingAsync(
                organizationId,
                ownerMemberId,
                conversationId,
                userMessageId,
                MessageProcessingStatus.Completed,
                "provider_unavailable",
                failedAt,
                CancellationToken.None));

        // Then
        Assert.Equal("failureStatus", exception.ParamName);
        Assert.Empty(dbContext.Messages);
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
