using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryListMessagesTests
{
    [Fact]
    public async Task Given_MessagesFromAnotherConversation_When_ListMessagesAsync_Then_ExcludesThem()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var otherConversationId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;
        var ownMessage = CreateMessage(conversationId, baseTime);

        await using var dbContext = CreateDbContext();
        dbContext.Messages.Add(ownMessage);
        dbContext.Messages.Add(CreateMessage(otherConversationId, baseTime));
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListMessagesAsync(
            conversationId, limit: 50, cursorCreatedAt: null, cursorId: null);

        // Then
        Assert.Single(page.Items);
        Assert.Equal(ownMessage.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task Given_MoreMessagesThanTheLimit_When_ListMessagesAsync_Then_ReturnsOnlyTheLimitAndFlagsHasMore()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;

        await using var dbContext = CreateDbContext();
        for (var i = 0; i < 5; i++)
        {
            dbContext.Messages.Add(CreateMessage(conversationId, baseTime.AddMinutes(i)));
        }

        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListMessagesAsync(
            conversationId, limit: 3, cursorCreatedAt: null, cursorId: null);

        // Then
        Assert.Equal(3, page.Items.Count);
        Assert.True(page.HasMore);
    }

    [Fact]
    public async Task Given_TheLastPage_When_ListMessagesAsync_Then_HasMoreIsFalseAndCursorFieldsAreNull()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;

        await using var dbContext = CreateDbContext();
        for (var i = 0; i < 3; i++)
        {
            dbContext.Messages.Add(CreateMessage(conversationId, baseTime.AddMinutes(i)));
        }

        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListMessagesAsync(
            conversationId, limit: 50, cursorCreatedAt: null, cursorId: null);

        // Then
        Assert.Equal(3, page.Items.Count);
        Assert.False(page.HasMore);
        Assert.Null(page.NextCursorCreatedAt);
        Assert.Null(page.NextCursorId);
    }

    [Fact]
    public async Task Given_MessagesWithDifferentDates_When_ListMessagesAsync_Then_ReturnsThemInChronologicalOrder()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;
        var oldest = CreateMessage(conversationId, baseTime);
        var newest = CreateMessage(conversationId, baseTime.AddMinutes(10));
        var middle = CreateMessage(conversationId, baseTime.AddMinutes(5));

        await using var dbContext = CreateDbContext();
        dbContext.Messages.AddRange(oldest, newest, middle);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListMessagesAsync(
            conversationId, limit: 50, cursorCreatedAt: null, cursorId: null);

        // Then
        Assert.Equal([oldest.Id, middle.Id, newest.Id], page.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Given_TwoMessagesWithTheSameCreatedAt_When_ListMessagesAsync_Then_BreaksTiesByIdAscending()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var sharedCreatedAt = DateTimeOffset.UtcNow;
        var lowerId = CreateMessage(
            conversationId, sharedCreatedAt, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higherId = CreateMessage(
            conversationId, sharedCreatedAt, id: Guid.Parse("00000000-0000-0000-0000-000000000002"));

        await using var dbContext = CreateDbContext();
        dbContext.Messages.AddRange(higherId, lowerId);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListMessagesAsync(
            conversationId, limit: 50, cursorCreatedAt: null, cursorId: null);

        // Then
        Assert.Equal([lowerId.Id, higherId.Id], page.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Given_ACursorFromThePreviousPage_When_ListMessagesAsync_Then_ReturnsNoDuplicatesAndNoGaps()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;
        var messages = Enumerable.Range(0, 6)
            .Select(i => CreateMessage(conversationId, baseTime.AddMinutes(i)))
            .ToList();

        await using var dbContext = CreateDbContext();
        dbContext.Messages.AddRange(messages);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When: first page (most recent 3, i.e. minutes 3,4,5) then the older page
        var firstPage = await repository.ListMessagesAsync(
            conversationId, limit: 3, cursorCreatedAt: null, cursorId: null);
        var secondPage = await repository.ListMessagesAsync(
            conversationId,
            limit: 3,
            cursorCreatedAt: firstPage.NextCursorCreatedAt,
            cursorId: firstPage.NextCursorId);

        // Then
        Assert.True(firstPage.HasMore);
        Assert.False(secondPage.HasMore);
        var allIds = secondPage.Items.Concat(firstPage.Items).Select(item => item.Id).ToList();
        Assert.Equal(6, allIds.Distinct().Count());
        Assert.Equal(
            messages.OrderBy(message => message.CreatedAt).Select(message => message.Id),
            allIds);
    }

    [Theory, AutoDomainData]
    public async Task Given_AMessageWithSources_When_ListMessagesAsync_Then_ReturnsOnlyItsOwnSources(
        Guid conversationId,
        Message messageWithSources,
        Message messageWithoutSources,
        MessageSource source)
    {
        // Given
        messageWithSources.ConversationId = conversationId;
        messageWithSources.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        messageWithoutSources.ConversationId = conversationId;
        messageWithoutSources.CreatedAt = DateTimeOffset.UtcNow;
        messageWithoutSources.Sources.Clear();
        source.MessageId = messageWithSources.Id;
        messageWithSources.Sources.Clear();
        messageWithSources.Sources.Add(source);

        await using var dbContext = CreateDbContext();
        dbContext.Messages.AddRange(messageWithSources, messageWithoutSources);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var page = await repository.ListMessagesAsync(
            conversationId, limit: 50, cursorCreatedAt: null, cursorId: null);

        // Then
        var withSources = page.Items.Single(item => item.Id == messageWithSources.Id);
        var withoutSources = page.Items.Single(item => item.Id == messageWithoutSources.Id);
        var returnedSource = Assert.Single(withSources.Sources);
        Assert.Equal(source.SourceType, returnedSource.SourceType);
        Assert.Equal(source.Title, returnedSource.Title);
        Assert.Equal(source.Reference, returnedSource.Reference);
        Assert.Equal(source.Url, returnedSource.Url);
        Assert.Equal(source.SourceDate, returnedSource.SourceDate);
        Assert.Empty(withoutSources.Sources);
    }

    private static Message CreateMessage(
        Guid conversationId,
        DateTimeOffset createdAt,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            ConversationId = conversationId,
            Role = MessageRole.User,
            Content = "Contenu du message",
            ProcessingStatus = MessageProcessingStatus.Completed,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
