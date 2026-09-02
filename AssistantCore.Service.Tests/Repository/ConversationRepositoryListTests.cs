using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryListTests
{
    [Theory, AutoDomainData]
    public async Task Given_ConversationsFromAnotherOrganization_When_ListConversationsAsync_Then_ExcludesThem(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation ownConversation,
        Conversation foreignConversation)
    {
        // Given
        ownConversation.OrganizationId = organizationId;
        ownConversation.OwnerMemberId = ownerMemberId;
        ownConversation.Status = ConversationStatus.Active;
        foreignConversation.Status = ConversationStatus.Active;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(ownConversation, foreignConversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Active, limit: 25, cursorUpdatedAt: null, cursorId: null);

        // Then
        Assert.Single(page.Items);
        Assert.Equal(ownConversation.Id, page.Items[0].Id);
    }

    [Theory, AutoDomainData]
    public async Task Given_ConversationsFromAnotherOwner_When_ListConversationsAsync_Then_ExcludesThem(
        Guid organizationId,
        Guid ownerMemberId,
        Guid otherOwnerMemberId,
        Conversation ownConversation,
        Conversation otherOwnerConversation)
    {
        // Given
        ownConversation.OrganizationId = organizationId;
        ownConversation.OwnerMemberId = ownerMemberId;
        ownConversation.Status = ConversationStatus.Active;
        otherOwnerConversation.OrganizationId = organizationId;
        otherOwnerConversation.OwnerMemberId = otherOwnerMemberId;
        otherOwnerConversation.Status = ConversationStatus.Active;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(ownConversation, otherOwnerConversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Active, limit: 25, cursorUpdatedAt: null, cursorId: null);

        // Then
        Assert.Single(page.Items);
        Assert.Equal(ownConversation.Id, page.Items[0].Id);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnArchivedConversation_When_ListConversationsAsync_Then_ExcludesIt(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation activeConversation,
        Conversation archivedConversation)
    {
        // Given
        activeConversation.OrganizationId = organizationId;
        activeConversation.OwnerMemberId = ownerMemberId;
        activeConversation.Status = ConversationStatus.Active;
        archivedConversation.OrganizationId = organizationId;
        archivedConversation.OwnerMemberId = ownerMemberId;
        archivedConversation.Status = ConversationStatus.Archived;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(activeConversation, archivedConversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Active, limit: 25, cursorUpdatedAt: null, cursorId: null);

        // Then
        Assert.Single(page.Items);
        Assert.Equal(activeConversation.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task Given_MoreConversationsThanTheLimit_When_ListConversationsAsync_Then_ReturnsOnlyTheLimitAndFlagsHasMore()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;

        await using var dbContext = CreateDbContext();
        for (var i = 0; i < 5; i++)
        {
            dbContext.Conversations.Add(CreateConversation(
                organizationId, ownerMemberId, updatedAt: baseTime.AddMinutes(i)));
        }

        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Active, limit: 3, cursorUpdatedAt: null, cursorId: null);

        // Then
        Assert.Equal(3, page.Items.Count);
        Assert.True(page.HasMore);
    }

    [Fact]
    public async Task Given_TheLastPage_When_ListConversationsAsync_Then_HasMoreIsFalse()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;

        await using var dbContext = CreateDbContext();
        for (var i = 0; i < 3; i++)
        {
            dbContext.Conversations.Add(CreateConversation(
                organizationId, ownerMemberId, updatedAt: baseTime.AddMinutes(i)));
        }

        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Active, limit: 25, cursorUpdatedAt: null, cursorId: null);

        // Then
        Assert.Equal(3, page.Items.Count);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task Given_ConversationsSortedByUpdatedAt_When_ListConversationsAsync_Then_OrdersMostRecentFirst()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;
        var oldest = CreateConversation(organizationId, ownerMemberId, baseTime);
        var newest = CreateConversation(organizationId, ownerMemberId, baseTime.AddMinutes(10));
        var middle = CreateConversation(organizationId, ownerMemberId, baseTime.AddMinutes(5));

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(oldest, newest, middle);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Active, limit: 25, cursorUpdatedAt: null, cursorId: null);

        // Then
        Assert.Equal([newest.Id, middle.Id, oldest.Id], page.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Given_TwoConversationsWithTheSameUpdatedAt_When_ListConversationsAsync_Then_BreaksTiesByIdDescending()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var sharedUpdatedAt = DateTimeOffset.UtcNow;
        var lowerId = CreateConversation(organizationId, ownerMemberId, sharedUpdatedAt, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higherId = CreateConversation(organizationId, ownerMemberId, sharedUpdatedAt, id: Guid.Parse("00000000-0000-0000-0000-000000000002"));

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(lowerId, higherId);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Active, limit: 25, cursorUpdatedAt: null, cursorId: null);

        // Then
        Assert.Equal([higherId.Id, lowerId.Id], page.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Given_ACursorFromThePreviousPage_When_ListConversationsAsync_Then_ReturnsNoDuplicatesAndNoGaps()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;
        var conversations = Enumerable.Range(0, 6)
            .Select(i => CreateConversation(organizationId, ownerMemberId, baseTime.AddMinutes(i)))
            .ToList();

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(conversations);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var firstPage = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Active, limit: 3, cursorUpdatedAt: null, cursorId: null);
        var lastOfFirstPage = firstPage.Items[^1];
        var secondPage = await repository.ListConversationsAsync(
            organizationId,
            ownerMemberId,
            ConversationStatus.Active,
            limit: 3,
            cursorUpdatedAt: lastOfFirstPage.UpdatedAt,
            cursorId: lastOfFirstPage.Id);

        // Then
        var allIds = firstPage.Items.Concat(secondPage.Items).Select(item => item.Id).ToList();
        Assert.Equal(6, allIds.Distinct().Count());
        Assert.Equal(
            conversations.OrderByDescending(c => c.UpdatedAt).Select(c => c.Id),
            allIds);
        Assert.False(secondPage.HasMore);
    }

    [Theory, AutoDomainData]
    public async Task Given_MessagesInAConversation_When_ListConversationsAsync_Then_PreviewIsTheMostRecentMessage(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Message olderMessage,
        Message newerMessage)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Active;
        olderMessage.ConversationId = conversation.Id;
        olderMessage.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        newerMessage.ConversationId = conversation.Id;
        newerMessage.CreatedAt = DateTimeOffset.UtcNow;
        conversation.Messages.Add(olderMessage);
        conversation.Messages.Add(newerMessage);

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Active, limit: 25, cursorUpdatedAt: null, cursorId: null);

        // Then
        Assert.Equal(newerMessage.Content, page.Items[0].LastMessageContent);
    }

    [Theory, AutoDomainData]
    public async Task Given_AConversationWithoutAnyMessage_When_ListConversationsAsync_Then_PreviewIsNull(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Active;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Active, limit: 25, cursorUpdatedAt: null, cursorId: null);

        // Then
        Assert.Null(page.Items[0].LastMessageContent);
    }

    private static Conversation CreateConversation(
        Guid organizationId,
        Guid ownerMemberId,
        DateTimeOffset updatedAt,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerMemberId = ownerMemberId,
            Title = "Conversation",
            Status = ConversationStatus.Active,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt
        };

    [Theory, AutoDomainData]
    public async Task Given_TheArchivedStatus_When_ListConversationsAsync_Then_ReturnsOnlyArchivedConversations(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation activeConversation,
        Conversation archivedConversation)
    {
        // Given
        activeConversation.OrganizationId = organizationId;
        activeConversation.OwnerMemberId = ownerMemberId;
        activeConversation.Status = ConversationStatus.Active;
        archivedConversation.OrganizationId = organizationId;
        archivedConversation.OwnerMemberId = ownerMemberId;
        archivedConversation.Status = ConversationStatus.Archived;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(activeConversation, archivedConversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Archived, limit: 25, cursorUpdatedAt: null, cursorId: null);

        // Then
        Assert.Single(page.Items);
        Assert.Equal(archivedConversation.Id, page.Items[0].Id);
    }

    [Theory, AutoDomainData]
    public async Task Given_AConversation_When_ListConversationsAsync_Then_ProjectsItsStatusAndVersion(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Archived;
        conversation.Version = 9;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId, ownerMemberId, ConversationStatus.Archived, limit: 25, cursorUpdatedAt: null, cursorId: null);

        // Then
        Assert.Equal(ConversationStatus.Archived, page.Items[0].Status);
        Assert.Equal(9, page.Items[0].Version);
    }

    [Theory, AutoDomainData]
    public async Task Given_ACursorProducedForOneStatus_When_ListConversationsAsync_Then_TheOtherStatusNeverLeaks(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation archivedConversation,
        Conversation olderActiveConversation)
    {
        // Given
        archivedConversation.OrganizationId = organizationId;
        archivedConversation.OwnerMemberId = ownerMemberId;
        archivedConversation.Status = ConversationStatus.Archived;
        archivedConversation.UpdatedAt = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        olderActiveConversation.OrganizationId = organizationId;
        olderActiveConversation.OwnerMemberId = ownerMemberId;
        olderActiveConversation.Status = ConversationStatus.Active;
        olderActiveConversation.UpdatedAt = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(archivedConversation, olderActiveConversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListConversationsAsync(
            organizationId,
            ownerMemberId,
            ConversationStatus.Archived,
            limit: 25,
            cursorUpdatedAt: archivedConversation.UpdatedAt,
            cursorId: archivedConversation.Id);

        // Then
        Assert.Empty(page.Items);
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
