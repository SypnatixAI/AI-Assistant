using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Services.Conversations.Audit;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Conversations;

public sealed class ConversationLifecycleService(
    IConversationRepository conversationRepository,
    IConversationAuditWriter auditWriter,
    IOptions<ConversationOptions> conversationOptions,
    IOptions<RetentionOptions> retentionOptions,
    TimeProvider timeProvider) : IConversationLifecycleService
{
    public async Task<ConversationResponse> UpdateAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        string? title,
        string? status,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (title is null && status is null)
        {
            throw new BadRequestException(
                "At least one of 'title' or 'status' must be provided.");
        }

        var desiredTitle = title is null ? null : NormalizeTitle(title);
        ConversationStatus? desiredStatus = status is null
            ? null
            : ConversationStatusParser.Parse(status);

        var conversation = await conversationRepository.FindConversationAsync(
            organizationId,
            ownerMemberId,
            conversationId,
            cancellationToken)
            ?? throw CreateNotFoundException();

        if (expectedVersion is not null && conversation.Version != expectedVersion)
        {
            throw CreateVersionConflictException();
        }

        var renames = desiredTitle is not null
            && !string.Equals(desiredTitle, conversation.Title, StringComparison.Ordinal);
        var changesStatus = desiredStatus is not null
            && desiredStatus.Value != conversation.Status;

        if (!renames && !changesStatus)
        {
            return Map(conversation);
        }

        var now = timeProvider.GetUtcNow();
        var result = await conversationRepository.UpdateConversationAsync(
            organizationId,
            ownerMemberId,
            conversationId,
            expectedVersion,
            renames ? desiredTitle : null,
            changesStatus ? desiredStatus : null,
            now,
            cancellationToken);

        var updated = result.Status switch
        {
            ConversationUpdateStatus.Updated when result.Conversation is not null =>
                result.Conversation,
            ConversationUpdateStatus.VersionConflict => throw CreateVersionConflictException(),
            _ => throw CreateNotFoundException()
        };

        await RecordChangesAsync(
            organizationId,
            ownerMemberId,
            conversationId,
            renames,
            changesStatus ? desiredStatus : null,
            now,
            cancellationToken);

        return Map(updated);
    }

    public async Task<bool> DeleteAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var purgeAfter = now.AddDays(retentionOptions.Value.ConversationRecoveryDays);

        var status = await conversationRepository.SoftDeleteConversationAsync(
            organizationId,
            ownerMemberId,
            conversationId,
            now,
            purgeAfter,
            cancellationToken);

        if (status == ConversationDeleteStatus.NotFound)
        {
            throw CreateNotFoundException();
        }

        if (status == ConversationDeleteStatus.AlreadyDeleted)
        {
            return true;
        }

        await auditWriter.RecordAsync(
            new ConversationAuditEntry(
                organizationId,
                ownerMemberId,
                conversationId,
                ConversationAuditAction.Deleted,
                now),
            cancellationToken);

        return false;
    }

    private async Task RecordChangesAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        bool renamed,
        ConversationStatus? newStatus,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (renamed)
        {
            await auditWriter.RecordAsync(
                new ConversationAuditEntry(
                    organizationId,
                    ownerMemberId,
                    conversationId,
                    ConversationAuditAction.Renamed,
                    occurredAt),
                cancellationToken);
        }

        if (newStatus is null)
        {
            return;
        }

        await auditWriter.RecordAsync(
            new ConversationAuditEntry(
                organizationId,
                ownerMemberId,
                conversationId,
                newStatus.Value == ConversationStatus.Archived
                    ? ConversationAuditAction.Archived
                    : ConversationAuditAction.Restored,
                occurredAt),
            cancellationToken);
    }

    private string NormalizeTitle(string title)
    {
        var normalized = ConversationTitleFactory.Normalize(title);
        var maximumLength = conversationOptions.Value.MaximumTitleLength;

        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new BadRequestException(
                $"Title must contain between 1 and {maximumLength} characters.");
        }

        return normalized;
    }

    private static ConversationResponse Map(Conversation conversation) =>
        new(
            conversation.Id,
            conversation.Title,
            conversation.Status.ToString(),
            conversation.UpdatedAt,
            conversation.Version);

    private static NotFoundException CreateNotFoundException() =>
        new("Conversation not found.");

    private static ConflictException CreateVersionConflictException() =>
        new(
            "The conversation was modified in another session.",
            ConflictException.ConversationVersionConflict);
}
