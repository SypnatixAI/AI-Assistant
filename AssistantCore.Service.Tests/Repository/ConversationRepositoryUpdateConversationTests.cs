using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class ConversationRepositoryUpdateConversationTests
{
    private const string CorrelationId = "request-8f812";

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
        var administrativeAuditRepository = new RecordingAdministrativeAuditRepository();
        var repository = new ConversationRepository(dbContext, administrativeAuditRepository);

        // When
        var result = await repository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            expectedVersion: 7,
            title: "Politique de teletravail",
            status: ConversationStatus.Archived,
            updatedAt,
            CorrelationId);

        // Then
        Assert.Equal(ConversationUpdateStatus.Updated, result.Status);
        Assert.NotNull(result.Conversation);
        Assert.Equal("Politique de teletravail", result.Conversation.Title);
        Assert.Equal(ConversationStatus.Archived, result.Conversation.Status);
        Assert.Equal(8, result.Conversation.Version);
        Assert.Equal(updatedAt, result.Conversation.UpdatedAt);

        var entry = Assert.Single(administrativeAuditRepository.StagedEntries);
        Assert.Equal(organizationId, entry.OrganizationId);
        Assert.Equal(AdministrativeAuditAction.ConversationArchived, entry.Action);
        Assert.Equal(ownerMemberId, entry.ActorId);
        Assert.Equal(conversation.Id, entry.TargetId);
        Assert.Equal(updatedAt, entry.OccurredAt);
        Assert.Equal(CorrelationId, entry.CorrelationId);
        Assert.Contains("\"status\":\"Active\"", entry.OldValues);
        Assert.Contains("\"status\":\"Archived\"", entry.NewValues);
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
        var repository = new ConversationRepository(dbContext, new StubAdministrativeAuditRepository());

        // When
        var result = await repository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            expectedVersion: 7,
            title: "Titre concurrent",
            status: null,
            updatedAt,
            CorrelationId);

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
        var repository = new ConversationRepository(dbContext, new StubAdministrativeAuditRepository());

        // When
        var result = await repository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            expectedVersion: null,
            title: "Budget marketing 2027",
            status: null,
            updatedAt,
            CorrelationId);

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
        var repository = new ConversationRepository(dbContext, new StubAdministrativeAuditRepository());

        // When
        var result = await repository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            expectedVersion: null,
            title: "Nouveau titre",
            status: null,
            updatedAt,
            CorrelationId);

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
        var repository = new ConversationRepository(dbContext, new StubAdministrativeAuditRepository());

        // When
        var result = await repository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            expectedVersion: null,
            title: "Titre vole",
            status: null,
            updatedAt,
            CorrelationId);

        // Then
        Assert.Equal(ConversationUpdateStatus.NotFound, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_ARenameWithoutStatusChange_When_UpdateConversationAsync_Then_DoesNotCreateAnAuditEntry(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset updatedAt)
    {
        // Given
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;
        conversation.Status = ConversationStatus.Active;
        conversation.Version = 1;

        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        var administrativeAuditRepository = new RecordingAdministrativeAuditRepository();
        var repository = new ConversationRepository(dbContext, administrativeAuditRepository);

        // When
        await repository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            expectedVersion: null,
            title: "Nouveau titre",
            status: null,
            updatedAt,
            CorrelationId);

        // Then
        Assert.Empty(administrativeAuditRepository.StagedEntries);
    }

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}
