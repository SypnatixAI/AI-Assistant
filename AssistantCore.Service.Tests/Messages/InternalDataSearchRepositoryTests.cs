using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Service.Application.Models.Messages.Connectors.InternalData;
using AssistantCore.Service.Infrastructure.Connectors.InternalData;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Messages;

public sealed class InternalDataSearchRepositoryTests
{
    [Theory, AutoDomainData]
    public async Task Given_MessagesFromMultipleContexts_When_SearchAsync_Then_ReturnsOnlyOwnedOrganizationMessages(
        Guid organizationId,
        Guid memberId,
        string query,
        DateTimeOffset updatedAt)
    {
        // Given
        var expectedConversation = CreateConversation(organizationId, memberId, "Expected", updatedAt);
        var expectedMessage = CreateMessage(expectedConversation, $"Expected {query}", updatedAt);
        var otherOrganizationConversation = CreateConversation(Guid.NewGuid(), memberId, "Other organization", updatedAt);
        var otherOrganizationMessage = CreateMessage(otherOrganizationConversation, query, updatedAt);
        var otherMemberConversation = CreateConversation(organizationId, Guid.NewGuid(), "Other member", updatedAt);
        var otherMemberMessage = CreateMessage(otherMemberConversation, query, updatedAt);
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(
            expectedConversation,
            otherOrganizationConversation,
            otherMemberConversation);
        dbContext.Messages.AddRange(
            expectedMessage,
            otherOrganizationMessage,
            otherMemberMessage);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new InternalDataSearchRepository(dbContext);
        var parameters = CreateParameters(
            organizationId,
            memberId,
            query,
            new HashSet<InternalDataCategory> { InternalDataCategory.Messages },
            maximumResults: 10);

        // When
        var results = await repository.SearchAsync(parameters, CancellationToken.None);

        // Then
        var result = Assert.Single(results);
        Assert.Equal(expectedMessage.Id.ToString(), result.Reference);
        Assert.Equal(InternalDataCategory.Messages, result.Category);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Theory, AutoDomainData]
    public async Task Given_OnlyConversationsAreConfigured_When_SearchAsync_Then_DoesNotReturnMessages(
        Guid organizationId,
        Guid memberId,
        string query,
        DateTimeOffset updatedAt)
    {
        // Given
        var conversation = CreateConversation(organizationId, memberId, query, updatedAt);
        var message = CreateMessage(conversation, query, updatedAt);
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();
        var repository = new InternalDataSearchRepository(dbContext);
        var parameters = CreateParameters(
            organizationId,
            memberId,
            query,
            new HashSet<InternalDataCategory> { InternalDataCategory.Conversations },
            maximumResults: 10);

        // When
        var results = await repository.SearchAsync(parameters, CancellationToken.None);

        // Then
        var result = Assert.Single(results);
        Assert.Equal(InternalDataCategory.Conversations, result.Category);
        Assert.Equal(conversation.Id.ToString(), result.Reference);
    }

    [Theory, AutoDomainData]
    public async Task Given_MoreMatchesThanAllowed_When_SearchAsync_Then_LimitsResults(
        Guid organizationId,
        Guid memberId,
        string query,
        DateTimeOffset updatedAt)
    {
        // Given
        var conversations = Enumerable.Range(0, 3)
            .Select(index => CreateConversation(
                organizationId,
                memberId,
                $"{query} {index}",
                updatedAt.AddMinutes(index)))
            .ToArray();
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(conversations);
        await dbContext.SaveChangesAsync();
        var repository = new InternalDataSearchRepository(dbContext);
        var parameters = CreateParameters(
            organizationId,
            memberId,
            query,
            new HashSet<InternalDataCategory> { InternalDataCategory.Conversations },
            maximumResults: 2);

        // When
        var results = await repository.SearchAsync(parameters, CancellationToken.None);

        // Then
        Assert.Equal(2, results.Count);
        Assert.Equal(
            conversations.OrderByDescending(conversation => conversation.UpdatedAt)
                .Take(2)
                .Select(conversation => conversation.Id.ToString()),
            results.Select(result => result.Reference));
    }

    private static InternalDataSearchParameters CreateParameters(
        Guid organizationId,
        Guid memberId,
        string query,
        IReadOnlySet<InternalDataCategory> categories,
        int maximumResults) => new(
            organizationId,
            memberId,
            query,
            categories,
            maximumResults);

    private static Conversation CreateConversation(
        Guid organizationId,
        Guid memberId,
        string title,
        DateTimeOffset updatedAt) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerMemberId = memberId,
            Title = title,
            Status = ConversationStatus.Active,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt
        };

    private static Message CreateMessage(
        Conversation conversation,
        string content,
        DateTimeOffset updatedAt) => new()
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Conversation = conversation,
            Role = MessageRole.Assistant,
            Content = content,
            ProcessingStatus = MessageProcessingStatus.Completed,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt
        };

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
