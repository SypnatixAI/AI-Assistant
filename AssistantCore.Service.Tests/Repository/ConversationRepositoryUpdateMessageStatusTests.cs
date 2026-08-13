using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryUpdateMessageStatusTests
{
    [Fact]
    public async Task Given_AnOwnedMessage_When_UpdateMessageProcessingStatusAsync_Then_UpdatesStatusAndDate()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var conversation = CreateConversation(organizationId, ownerMemberId);
        var message = CreateMessage(conversation.Id);
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var updated = await repository.UpdateMessageProcessingStatusAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            message.Id,
            MessageProcessingStatus.InProgress,
            updatedAt,
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedMessage = await dbContext.Messages.SingleAsync();
        Assert.True(updated);
        Assert.Equal(MessageProcessingStatus.InProgress, persistedMessage.ProcessingStatus);
        Assert.Equal(updatedAt, persistedMessage.UpdatedAt);
    }

    [Fact]
    public async Task Given_AWrongOrganization_When_UpdateMessageProcessingStatusAsync_Then_DoesNotModifyMessage()
    {
        // Given
        var conversation = CreateConversation(Guid.NewGuid(), Guid.NewGuid());
        var message = CreateMessage(conversation.Id);
        var originalUpdatedAt = message.UpdatedAt;
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var updated = await repository.UpdateMessageProcessingStatusAsync(
            Guid.NewGuid(),
            conversation.OwnerMemberId,
            conversation.Id,
            message.Id,
            MessageProcessingStatus.Failed,
            DateTimeOffset.UtcNow.AddMinutes(1),
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedMessage = await dbContext.Messages.SingleAsync();
        Assert.False(updated);
        Assert.Equal(MessageProcessingStatus.Pending, persistedMessage.ProcessingStatus);
        Assert.Equal(originalUpdatedAt, persistedMessage.UpdatedAt);
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }

    private static Conversation CreateConversation(Guid organizationId, Guid ownerMemberId) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        OwnerMemberId = ownerMemberId,
        Title = "Conversation",
        Status = ConversationStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static Message CreateMessage(Guid conversationId) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Role = MessageRole.User,
        Content = "Question",
        ProcessingStatus = MessageProcessingStatus.Pending,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
