using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryUpdateMessageStatusTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnOwnedMessage_When_UpdateMessageProcessingStatusAsync_Then_UpdatesStatusAndDate(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message message,
        DateTimeOffset updatedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        message.ConversationId = conversation.Id;
        message.Role = MessageRole.User;
        message.ProcessingStatus = MessageProcessingStatus.Pending;
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

    [Theory, AutoDomainData]
    public async Task Given_AWrongOrganization_When_UpdateMessageProcessingStatusAsync_Then_DoesNotModifyMessage(
        Guid organizationId,
        Guid ownerMemberId,
        Guid wrongOrganizationId,
        Conversation conversation,
        Message message,
        DateTimeOffset updatedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        message.ConversationId = conversation.Id;
        message.Role = MessageRole.User;
        message.ProcessingStatus = MessageProcessingStatus.Pending;
        var originalUpdatedAt = message.UpdatedAt;
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var updated = await repository.UpdateMessageProcessingStatusAsync(
            wrongOrganizationId,
            conversation.OwnerMemberId,
            conversation.Id,
            message.Id,
            MessageProcessingStatus.Failed,
            updatedAt,
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
}
