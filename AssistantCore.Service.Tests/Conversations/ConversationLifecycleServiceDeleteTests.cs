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

public sealed class ConversationLifecycleServiceDeleteTests
{
    private const int RecoveryDays = 30;

    [Theory, AutoDomainData]
    public async Task Given_AVisibleConversation_When_DeleteAsync_Then_SchedulesThePurgeAndAuditsTheDeletion(
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
        var alreadyDeleted = await service.DeleteAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            CancellationToken.None);

        // Then
        Assert.False(alreadyDeleted);
        var persisted = await dbContext.Conversations.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == conversation.Id);
        Assert.Equal(now, persisted.DeletedAt);
        var request = await dbContext.ConversationPurgeRequests.AsNoTracking()
            .SingleAsync(candidate => candidate.ConversationId == conversation.Id);
        Assert.Equal(now.AddDays(RecoveryDays), request.PurgeAfter);
        var entry = Assert.Single(auditWriter.Entries);
        Assert.Equal(ConversationAuditAction.Deleted, entry.Action);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnAlreadyDeletedConversation_When_DeleteAsync_Then_StaysIdempotentWithoutASecondAudit(
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
        await service.DeleteAsync(organizationId, ownerMemberId, conversation.Id, CancellationToken.None);

        // When
        var alreadyDeleted = await service.DeleteAsync(
            organizationId,
            ownerMemberId,
            conversation.Id,
            CancellationToken.None);

        // Then
        Assert.True(alreadyDeleted);
        Assert.Single(auditWriter.Entries);
        var requests = await dbContext.ConversationPurgeRequests.AsNoTracking()
            .Where(candidate => candidate.ConversationId == conversation.Id)
            .ToListAsync();
        Assert.Single(requests);
    }

    [Theory, AutoDomainData]
    public async Task Given_AConversationOfAnotherOwner_When_DeleteAsync_Then_ThrowsNotFoundException(
        Guid organizationId,
        Guid ownerMemberId,
        Guid otherOwnerMemberId,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        await using var dbContext = await SeedAsync(conversation, organizationId, otherOwnerMemberId);
        var auditWriter = new RecordingAuditWriter();
        var service = CreateService(dbContext, auditWriter, now);

        // When / Then
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteAsync(
                organizationId,
                ownerMemberId,
                conversation.Id,
                CancellationToken.None));
        Assert.Empty(auditWriter.Entries);
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
        DateTimeOffset now) =>
        new(
            new ConversationRepository(dbContext),
            auditWriter,
            Options.Create(new ConversationOptions { MaximumTitleLength = 200 }),
            Options.Create(new RetentionOptions { ConversationRecoveryDays = RecoveryDays }),
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
