using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryUpdateConversationTests
{
    [Theory, AutoDomainData]
    public async Task Given_AMatchingVersion_When_UpdateConversationAsync_Then_PersistsChangesAndIncrementsVersion(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset updatedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Active;
        conversation.Version = 7;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            expectedVersion: 7,
            title: "Politique de teletravail",
            status: ConversationStatus.Archived,
            updatedAt);

        // Then
        Assert.Equal(ConversationUpdateStatus.Updated, result.Status);
        Assert.NotNull(result.Conversation);
        Assert.Equal("Politique de teletravail", result.Conversation.Title);
        Assert.Equal(ConversationStatus.Archived, result.Conversation.Status);
        Assert.Equal(8, result.Conversation.Version);
        Assert.Equal(updatedAt, result.Conversation.UpdatedAt);
    }

    [Theory, AutoDomainData]
    public async Task Given_AStaleVersion_When_UpdateConversationAsync_Then_ReturnsVersionConflictWithoutWriting(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset updatedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Active;
        conversation.Title = "Titre initial";
        conversation.Version = 8;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            expectedVersion: 7,
            title: "Titre concurrent",
            status: null,
            updatedAt);

        // Then
        Assert.Equal(ConversationUpdateStatus.VersionConflict, result.Status);
        Assert.Null(result.Conversation);
        var persisted = await dbContext.Conversations.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == conversation.Id);
        Assert.Equal("Titre initial", persisted.Title);
        Assert.Equal(8, persisted.Version);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoExpectedVersion_When_UpdateConversationAsync_Then_AppliesTheChange(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset updatedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Active;
        conversation.Version = 3;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            expectedVersion: null,
            title: "Budget marketing 2027",
            status: null,
            updatedAt);

        // Then
        Assert.Equal(ConversationUpdateStatus.Updated, result.Status);
        Assert.Equal(4, result.Conversation!.Version);
    }

    [Theory, AutoDomainData]
    public async Task Given_ADeletedConversation_When_UpdateConversationAsync_Then_ReturnsNotFound(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset deletedAt,
        DateTimeOffset updatedAt)
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
        var result = await repository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            expectedVersion: null,
            title: "Nouveau titre",
            status: null,
            updatedAt);

        // Then
        Assert.Equal(ConversationUpdateStatus.NotFound, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AConversationOfAnotherOwner_When_UpdateConversationAsync_Then_ReturnsNotFound(
        Guid organizationId,
        Guid ownerMemberId,
        Guid otherOwnerMemberId,
        Conversation conversation,
        DateTimeOffset updatedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = otherOwnerMemberId;
        conversation.Status = ConversationStatus.Active;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var result = await repository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            expectedVersion: null,
            title: "Titre vole",
            status: null,
            updatedAt);

        // Then
        Assert.Equal(ConversationUpdateStatus.NotFound, result.Status);
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
