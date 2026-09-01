using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryDeletedVisibilityTests
{
    [Theory, AutoDomainData]
    public async Task Given_ADeletedConversation_When_FindConversationAsync_Then_ReturnsNull(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset deletedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Active;
        conversation.DeletedAt = deletedAt;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var found = await repository.FindConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id);

        // Then
        Assert.Null(found);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnArchivedConversation_When_FindConversationAsync_Then_StillReturnsIt(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Archived;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var found = await repository.FindConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id);

        // Then
        Assert.NotNull(found);
        Assert.Equal(ConversationStatus.Archived, found.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnArchivedConversation_When_ListMessagesAsync_Then_StillReturnsItsHistory(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message message)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Archived;
        message.ConversationId = conversation.Id;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListMessagesAsync(
            conversation.Id,
            limit: 25,
            cursorCreatedAt: null,
            cursorId: null);

        // Then
        var returned = Assert.Single(page.Items);
        Assert.Equal(message.Id, returned.Id);
    }

    [Theory, AutoDomainData]
    public async Task Given_ADeletedConversation_When_ListConversationsAsync_Then_ExcludesIt(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation visibleConversation,
        Conversation deletedConversation,
        DateTimeOffset deletedAt)
    {
        // Given
        visibleConversation.OrganizationId = organizationId;
        visibleConversation.OwnerMemberId = ownerMemberId;
        visibleConversation.Status = ConversationStatus.Active;
        deletedConversation.OrganizationId = organizationId;
        deletedConversation.OwnerMemberId = ownerMemberId;
        deletedConversation.Status = ConversationStatus.Active;
        deletedConversation.DeletedAt = deletedAt;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(visibleConversation, deletedConversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId,
            ownerMemberId,
            ConversationStatus.Active,
            limit: 25,
            cursorUpdatedAt: null,
            cursorId: null);

        // Then
        Assert.Single(page.Items);
        Assert.Equal(visibleConversation.Id, page.Items[0].Id);
    }

    [Theory, AutoDomainData]
    public async Task Given_ADeletedConversation_When_AddUserMessageAsync_Then_ReturnsNull(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message userMessage,
        DateTimeOffset deletedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Active;
        conversation.DeletedAt = deletedAt;
        userMessage.ConversationId = conversation.Id;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var added = await repository.AddUserMessageAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            userMessage);

        // Then
        Assert.Null(added);
    }

    [Theory, AutoDomainData]
    public async Task Given_ADeletedArchivedConversation_When_ListConversationsAsync_Then_ExcludesItFromTheArchivedList(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation visibleConversation,
        Conversation deletedConversation,
        DateTimeOffset deletedAt)
    {
        // Given
        visibleConversation.OrganizationId = organizationId;
        visibleConversation.OwnerMemberId = ownerMemberId;
        visibleConversation.Status = ConversationStatus.Archived;
        deletedConversation.OrganizationId = organizationId;
        deletedConversation.OwnerMemberId = ownerMemberId;
        deletedConversation.Status = ConversationStatus.Archived;
        deletedConversation.DeletedAt = deletedAt;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(visibleConversation, deletedConversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId,
            ownerMemberId,
            ConversationStatus.Archived,
            limit: 25,
            cursorUpdatedAt: null,
            cursorId: null);

        // Then
        Assert.Single(page.Items);
        Assert.Equal(visibleConversation.Id, page.Items[0].Id);
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
