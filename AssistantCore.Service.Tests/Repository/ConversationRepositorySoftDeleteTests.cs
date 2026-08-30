using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositorySoftDeleteTests
{
    [Theory, AutoDomainData]
    public async Task Given_AVisibleConversation_When_SoftDeleteConversationAsync_Then_MarksItDeletedAndSchedulesThePurge(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset deletedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Active;
        var purgeAfter = deletedAt.AddDays(30);

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);

        // When
        var status = await repository.SoftDeleteConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            deletedAt,
            purgeAfter);

        // Then
        Assert.Equal(ConversationDeleteStatus.Deleted, status);
        var persisted = await dbContext.Conversations.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == conversation.Id);
        Assert.Equal(deletedAt, persisted.DeletedAt);
        var request = await dbContext.ConversationPurgeRequests.AsNoTracking()
            .SingleAsync(candidate => candidate.ConversationId == conversation.Id);
        Assert.Equal(organizationId, request.OrganizationId);
        Assert.Equal(purgeAfter, request.PurgeAfter);
        Assert.Equal(ConversationPurgeStatus.Pending, request.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnAlreadyDeletedConversation_When_SoftDeleteConversationAsync_Then_KeepsASinglePurgeRequest(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset deletedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Active;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var repository = new ConversationRepository(dbContext);
        await repository.SoftDeleteConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            deletedAt,
            deletedAt.AddDays(30));

        // When
        var status = await repository.SoftDeleteConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            deletedAt.AddDays(1),
            deletedAt.AddDays(31));

        // Then
        Assert.Equal(ConversationDeleteStatus.AlreadyDeleted, status);
        var requests = await dbContext.ConversationPurgeRequests.AsNoTracking()
            .Where(candidate => candidate.ConversationId == conversation.Id)
            .ToListAsync();
        Assert.Single(requests);
        var persisted = await dbContext.Conversations.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == conversation.Id);
        Assert.Equal(deletedAt, persisted.DeletedAt);
    }

    [Theory, AutoDomainData]
    public async Task Given_AConversationOfAnotherOwner_When_SoftDeleteConversationAsync_Then_ReturnsNotFound(
        Guid organizationId,
        Guid ownerMemberId,
        Guid otherOwnerMemberId,
        Conversation conversation,
        DateTimeOffset deletedAt)
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
        var status = await repository.SoftDeleteConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            deletedAt,
            deletedAt.AddDays(30));

        // Then
        Assert.Equal(ConversationDeleteStatus.NotFound, status);
        Assert.Empty(dbContext.ConversationPurgeRequests);
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
