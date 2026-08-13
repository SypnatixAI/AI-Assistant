using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryCompleteMessageTests
{
    [Fact]
    public async Task Given_AValidatedAssistantResponse_When_CompleteMessageWithAssistantResponseAsync_Then_PersistsResponseSourcesAndCompletion()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var conversation = CreateConversation(organizationId, ownerMemberId);
        var userMessage = CreateUserMessage(conversation.Id);
        var assistantMessage = CreateAssistantMessage();
        var source = CreateSource();
        var completedAt = DateTimeOffset.UtcNow.AddMinutes(1);
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
            completedAt,
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedConversation = await dbContext.Conversations.SingleAsync();
        var persistedMessages = await dbContext.Messages
            .OrderBy(message => message.CreatedAt)
            .ToArrayAsync();
        var persistedSource = await dbContext.MessageSources.SingleAsync();
        Assert.Same(assistantMessage, result);
        Assert.Equal(MessageProcessingStatus.Completed, persistedMessages[0].ProcessingStatus);
        Assert.Equal(MessageRole.Assistant, persistedMessages[1].Role);
        Assert.Equal(conversation.Id, persistedMessages[1].ConversationId);
        Assert.Equal(assistantMessage.Id, persistedSource.MessageId);
        Assert.Equal(completedAt, persistedConversation.UpdatedAt);
    }

    [Fact]
    public async Task Given_AWrongOwner_When_CompleteMessageWithAssistantResponseAsync_Then_DoesNotPersistAssistantResponse()
    {
        // Given
        var conversation = CreateConversation(Guid.NewGuid(), Guid.NewGuid());
        var userMessage = CreateUserMessage(conversation.Id);
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(userMessage);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.CompleteMessageWithAssistantResponseAsync(
            conversation.OrganizationId,
            Guid.NewGuid(),
            conversation.Id,
            userMessage.Id,
            CreateAssistantMessage(),
            [CreateSource()],
            DateTimeOffset.UtcNow.AddMinutes(1),
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedUserMessage = await dbContext.Messages.SingleAsync();
        Assert.Null(result);
        Assert.Equal(MessageProcessingStatus.Pending, persistedUserMessage.ProcessingStatus);
        Assert.Empty(dbContext.MessageSources);
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

    private static Message CreateUserMessage(Guid conversationId) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Role = MessageRole.User,
        Content = "Question",
        ProcessingStatus = MessageProcessingStatus.InProgress,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static Message CreateAssistantMessage() => new()
    {
        Id = Guid.NewGuid(),
        Role = MessageRole.User,
        Content = "Reponse validee",
        ProcessingStatus = MessageProcessingStatus.Pending,
        Model = "gpt",
        CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1),
        UpdatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
    };

    private static MessageSource CreateSource() => new()
    {
        Id = Guid.NewGuid(),
        SourceType = "sharepoint",
        Title = "Politique",
        Reference = "document-1",
        Url = "https://example.com/document-1"
    };
}
