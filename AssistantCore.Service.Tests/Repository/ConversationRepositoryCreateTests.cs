using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryCreateTests
{
    [Fact]
    public async Task Given_AConversationAndFirstMessage_When_CreateConversationWithFirstMessageAsync_Then_PersistsBothInTheProvidedContext()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var conversation = CreateConversation();
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        var userMessage = CreateUserMessage(conversation.Id);
        await using var dbContext = CreateDbContext();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.CreateConversationWithFirstMessageAsync(
            organizationId,
            ownerMemberId,
            conversation,
            userMessage,
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedConversation = await dbContext.Conversations.SingleAsync();
        var persistedMessage = await dbContext.Messages.SingleAsync();
        Assert.Same(conversation, result.Conversation);
        Assert.Same(userMessage, result.UserMessage);
        Assert.Equal(organizationId, persistedConversation.OrganizationId);
        Assert.Equal(ownerMemberId, persistedConversation.OwnerMemberId);
        Assert.Equal(persistedConversation.Id, persistedMessage.ConversationId);
        Assert.Equal(MessageRole.User, persistedMessage.Role);
        Assert.Equal(MessageProcessingStatus.Pending, persistedMessage.ProcessingStatus);
    }

    [Fact]
    public async Task Given_EmptyContextIdentifiers_When_CreateConversationWithFirstMessageAsync_Then_AssignsProvidedContext()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var conversation = CreateConversation();
        conversation.OrganizationId = Guid.Empty;
        conversation.OwnerMemberId = Guid.Empty;
        var userMessage = CreateUserMessage(Guid.Empty);
        await using var dbContext = CreateDbContext();
        var repository = new ConversationRepository(dbContext);

        // When
        await repository.CreateConversationWithFirstMessageAsync(
            organizationId,
            ownerMemberId,
            conversation,
            userMessage,
            CancellationToken.None);

        // Then
        Assert.Equal(organizationId, conversation.OrganizationId);
        Assert.Equal(ownerMemberId, conversation.OwnerMemberId);
        Assert.Equal(conversation.Id, userMessage.ConversationId);
    }

    [Theory]
    [InlineData("organization")]
    [InlineData("owner")]
    [InlineData("conversation")]
    public async Task Given_AConflictingIdentifier_When_CreateConversationWithFirstMessageAsync_Then_ThrowsWithoutPersisting(
        string conflictingIdentifier)
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var conversation = CreateConversation();
        conversation.OrganizationId = conflictingIdentifier == "organization"
            ? Guid.NewGuid()
            : organizationId;
        conversation.OwnerMemberId = conflictingIdentifier == "owner"
            ? Guid.NewGuid()
            : ownerMemberId;
        var userMessage = CreateUserMessage(
            conflictingIdentifier == "conversation"
                ? Guid.NewGuid()
                : conversation.Id);
        await using var dbContext = CreateDbContext();
        var repository = new ConversationRepository(dbContext);

        // When
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.CreateConversationWithFirstMessageAsync(
                organizationId,
                ownerMemberId,
                conversation,
                userMessage,
                CancellationToken.None));

        // Then
        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
        Assert.Empty(dbContext.Conversations);
        Assert.Empty(dbContext.Messages);
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }

    private static Conversation CreateConversation() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        OwnerMemberId = Guid.NewGuid(),
        Title = "Question initiale",
        Status = ConversationStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static Message CreateUserMessage(Guid conversationId) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Role = MessageRole.Assistant,
        Content = "Question",
        ProcessingStatus = MessageProcessingStatus.Completed,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
