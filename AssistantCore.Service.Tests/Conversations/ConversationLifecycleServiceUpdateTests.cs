using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Conversations;
using AssistantCore.Service.Application.Services.Conversations.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class ConversationLifecycleServiceUpdateTests
{
    [Theory, AutoDomainData]
    public async Task Given_ANewTitle_When_UpdateAsync_Then_RenamesAndAuditsTheChange(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        conversation.Title = "Ancien titre";
        conversation.Version = 7;
        await using var dbContext = await SeedAsync(conversation, organizationId, ownerMemberId);
        var auditWriter = new RecordingAuditWriter();
        var service = CreateService(dbContext, auditWriter, now);

        // When
        var response = await service.UpdateAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            "  Politique   de teletravail  ",
            null,
            null,
            CancellationToken.None);

        // Then
        Assert.Equal("Politique de teletravail", response.Title);
        Assert.Equal(8, response.Version);
        Assert.Equal(now, response.UpdatedAt);
        var entry = Assert.Single(auditWriter.Entries);
        Assert.Equal(ConversationAuditAction.Renamed, entry.Action);
        Assert.Equal(conversation.Id, entry.ConversationId);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheArchivedStatus_When_UpdateAsync_Then_ArchivesAndAuditsTheChange(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        conversation.Status = ConversationStatus.Active;
        await using var dbContext = await SeedAsync(conversation, organizationId, ownerMemberId);
        var auditWriter = new RecordingAuditWriter();
        var service = CreateService(dbContext, auditWriter, now);

        // When
        var response = await service.UpdateAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            null,
            nameof(ConversationStatus.Archived),
            null,
            CancellationToken.None);

        // Then
        Assert.Equal(nameof(ConversationStatus.Archived), response.Status);
        var entry = Assert.Single(auditWriter.Entries);
        Assert.Equal(ConversationAuditAction.Archived, entry.Action);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnArchivedConversation_When_UpdateAsync_Then_RestoresAndAuditsTheChange(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        conversation.Status = ConversationStatus.Archived;
        await using var dbContext = await SeedAsync(conversation, organizationId, ownerMemberId);
        var auditWriter = new RecordingAuditWriter();
        var service = CreateService(dbContext, auditWriter, now);

        // When
        var response = await service.UpdateAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            null,
            nameof(ConversationStatus.Active),
            null,
            CancellationToken.None);

        // Then
        Assert.Equal(nameof(ConversationStatus.Active), response.Status);
        var entry = Assert.Single(auditWriter.Entries);
        Assert.Equal(ConversationAuditAction.Restored, entry.Action);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnIdenticalTitle_When_UpdateAsync_Then_ChangesNothingAndWritesNoAudit(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        conversation.Title = "Budget marketing 2027";
        conversation.Version = 4;
        await using var dbContext = await SeedAsync(conversation, organizationId, ownerMemberId);
        var auditWriter = new RecordingAuditWriter();
        var service = CreateService(dbContext, auditWriter, now);

        // When
        var response = await service.UpdateAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            "  Budget   marketing 2027 ",
            null,
            null,
            CancellationToken.None);

        // Then
        Assert.Equal(4, response.Version);
        Assert.Empty(auditWriter.Entries);
        var persisted = await dbContext.Conversations.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == conversation.Id);
        Assert.Equal(4, persisted.Version);
    }

    [Theory, AutoDomainData]
    public async Task Given_AStaleExpectedVersion_When_UpdateAsync_Then_ThrowsAVersionConflict(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        conversation.Version = 8;
        await using var dbContext = await SeedAsync(conversation, organizationId, ownerMemberId);
        var service = CreateService(dbContext, new RecordingAuditWriter(), now);

        // When
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAsync(
                organizationId,
                ownerMemberId,
                conversation.Id,
                "Titre concurrent",
                null,
                7,
                CancellationToken.None));

        // Then
        Assert.Equal(ConflictException.ConversationVersionConflict, exception.ErrorCode);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyPatch_When_UpdateAsync_Then_ThrowsBadRequestException(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        await using var dbContext = await SeedAsync(conversation, organizationId, ownerMemberId);
        var service = CreateService(dbContext, new RecordingAuditWriter(), now);

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateAsync(
                organizationId,
                ownerMemberId,
                conversation.Id,
                null,
                null,
                null,
                CancellationToken.None));
    }

    [Theory]
    [InlineAutoDomainData("Deleted")]
    [InlineAutoDomainData("active")]
    [InlineAutoDomainData("")]
    public async Task Given_AnUnsupportedStatus_When_UpdateAsync_Then_ThrowsBadRequestException(
        string status,
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        await using var dbContext = await SeedAsync(conversation, organizationId, ownerMemberId);
        var service = CreateService(dbContext, new RecordingAuditWriter(), now);

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateAsync(
                organizationId,
                ownerMemberId,
                conversation.Id,
                null,
                status,
                null,
                CancellationToken.None));
    }

    [Theory]
    [InlineAutoDomainData("   ")]
    [InlineAutoDomainData("")]
    public async Task Given_ABlankTitle_When_UpdateAsync_Then_ThrowsBadRequestException(
        string title,
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        await using var dbContext = await SeedAsync(conversation, organizationId, ownerMemberId);
        var service = CreateService(dbContext, new RecordingAuditWriter(), now);

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateAsync(
                organizationId,
                ownerMemberId,
                conversation.Id,
                title,
                null,
                null,
                CancellationToken.None));
    }

    [Theory, AutoDomainData]
    public async Task Given_ATitleLongerThanTheConfiguredLimit_When_UpdateAsync_Then_ThrowsBadRequestException(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        await using var dbContext = await SeedAsync(conversation, organizationId, ownerMemberId);
        var service = CreateService(dbContext, new RecordingAuditWriter(), now, maximumTitleLength: 10);

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateAsync(
                organizationId,
                ownerMemberId,
                conversation.Id,
                new string('a', 11),
                null,
                null,
                CancellationToken.None));
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnknownConversation_When_UpdateAsync_Then_ThrowsNotFoundException(
        Guid organizationId,
        Guid ownerMemberId,
        Conversation conversation,
        Guid unknownConversationId,
        DateTimeOffset now)
    {
        // Given
        await using var dbContext = await SeedAsync(conversation, organizationId, ownerMemberId);
        var service = CreateService(dbContext, new RecordingAuditWriter(), now);

        // When / Then
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateAsync(
                organizationId,
                ownerMemberId,
                unknownConversationId,
                "Titre",
                null,
                null,
                CancellationToken.None));
    }

    private static async Task<AssistantCoreDbContext> SeedAsync(
        Conversation conversation,
        Guid organizationId,
        Guid ownerMemberId)
    {
        conversation.OrganizationId = organizationId;
        conversation.OwnerMemberId = ownerMemberId;

        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new AssistantCoreDbContext(options);
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return dbContext;
    }

    private static ConversationLifecycleService CreateService(
        AssistantCoreDbContext dbContext,
        IConversationAuditWriter auditWriter,
        DateTimeOffset now,
        int maximumTitleLength = 200) =>
        new(
            new ConversationRepository(dbContext),
            auditWriter,
            Options.Create(new ConversationOptions { MaximumTitleLength = maximumTitleLength }),
            Options.Create(new RetentionOptions { ConversationRecoveryDays = 30 }),
            new StubTimeProvider(now));

    private sealed class RecordingAuditWriter : IConversationAuditWriter
    {
        public List<ConversationAuditEntry> Entries { get; } = [];

        public Task RecordAsync(ConversationAuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
