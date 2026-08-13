using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryFindTests
{
    [Theory, AutoDomainData]
    public async Task Given_ConversationsFromMultipleContexts_When_FindConversationAsync_Then_ReturnsOnlyTheOwnedConversation(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation expectedConversation,
        Conversation otherConversation)
    {
        // Given
        expectedConversation.OrganizationId = organizationId;
        expectedConversation.OwnerMemberId = ownerMemberId;
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.AddRange(expectedConversation, otherConversation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.FindConversationAsync(
            organizationId,
            ownerMemberId,
            expectedConversation.Id,
            CancellationToken.None);

        // Then
        Assert.NotNull(result);
        Assert.Equal(expectedConversation.Id, result.Id);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Theory]
    [InlineAutoDomainData(true, false)]
    [InlineAutoDomainData(false, true)]
    public async Task Given_AnIncorrectContext_When_FindConversationAsync_Then_ReturnsNull(
        bool useWrongOrganization,
        bool useWrongOwner,
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.FindConversationAsync(
            useWrongOrganization ? Guid.NewGuid() : organizationId,
            useWrongOwner ? Guid.NewGuid() : ownerMemberId,
            conversation.Id,
            CancellationToken.None);

        // Then
        Assert.Null(result);
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
