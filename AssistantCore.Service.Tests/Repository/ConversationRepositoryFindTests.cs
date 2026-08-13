using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryFindTests
{
    [Fact]
    public async Task Given_ConversationsFromMultipleContexts_When_FindConversationAsync_Then_ReturnsOnlyTheOwnedConversation()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var expectedConversation = CreateConversation(organizationId, ownerMemberId);
        var otherConversation = CreateConversation(Guid.NewGuid(), Guid.NewGuid());
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
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Given_AnIncorrectContext_When_FindConversationAsync_Then_ReturnsNull(
        bool useWrongOrganization,
        bool useWrongOwner)
    {
        // Given
        var organizationId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var conversation = CreateConversation(organizationId, ownerMemberId);
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

    private static Conversation CreateConversation(
        Guid organizationId,
        Guid ownerMemberId) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        OwnerMemberId = ownerMemberId,
        Title = "Conversation",
        Status = ConversationStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
