using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryAddUserMessageTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnOwnedConversation_When_AddUserMessageAsync_Then_PersistsPendingUserMessage(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message userMessage)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        userMessage.ConversationId = Guid.Empty;
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.AddUserMessageAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            userMessage,
            CancellationToken.None);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedMessage = await dbContext.Messages.SingleAsync();
        Assert.Same(userMessage, result);
        Assert.Equal(conversation.Id, persistedMessage.ConversationId);
        Assert.Equal(MessageRole.User, persistedMessage.Role);
        Assert.Equal(MessageProcessingStatus.Pending, persistedMessage.ProcessingStatus);
    }

    [Theory]
    [InlineAutoDomainData("organization")]
    [InlineAutoDomainData("owner")]
    public async Task Given_AConversationOutsideTheProvidedContext_When_AddUserMessageAsync_Then_DoesNotPersistMessage(
        string mismatchedContext,
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message userMessage)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        userMessage.ConversationId = Guid.Empty;
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);
        var requestedOrganizationId = mismatchedContext == "organization"
            ? Guid.NewGuid()
            : organizationId;
        var requestedOwnerMemberId = mismatchedContext == "owner"
            ? Guid.NewGuid()
            : ownerMemberId;

        // When
        var result = await repository.AddUserMessageAsync(
            requestedOrganizationId,
            requestedOwnerMemberId,
            conversation.Id,
            userMessage,
            CancellationToken.None);

        // Then
        Assert.Null(result);
        Assert.Empty(dbContext.Messages);
    }

    [Theory, AutoDomainData]
    public async Task Given_AConflictingConversationIdentifier_When_AddUserMessageAsync_Then_ThrowsWithoutPersisting(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message userMessage)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        userMessage.ConversationId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.AddUserMessageAsync(
                organizationId,
                ownerMemberId,
                conversation.Id,
                userMessage,
                CancellationToken.None));

        // Then
        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
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
