using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AgentRuntime;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Services.Conversations;

namespace AssistantCore.Service.Application.Services.Messages.Lifecycle;

public sealed class MessageProcessingLifecycleService(
    IConversationRepository conversationRepository,
    TimeProvider timeProvider) : IMessageProcessingLifecycleService
{
    private const int MaximumProcessingErrorCodeLength = 100;

    public async Task<StartedMessageProcessing> StartAsync(
        Guid? conversationId,
        string message,
        Organization organization,
        OrganizationMember member,
        CancellationToken cancellationToken)
    {
        EnsureMemberBelongsToOrganization(organization, member);

        var now = timeProvider.GetUtcNow();
        var userMessage = CreateUserMessage(message, now);
        Conversation conversation;
        IReadOnlyCollection<AiConversationMessage> conversationHistory;

        if (conversationId is null)
        {
            conversation = await CreateConversationWithFirstMessageAsync(
                organization,
                member,
                userMessage,
                now,
                cancellationToken);
            conversationHistory = Array.Empty<AiConversationMessage>();
        }
        else
        {
            var existingConversation = await AddMessageToExistingConversationAsync(
                conversationId.Value,
                organization,
                member,
                userMessage,
                cancellationToken);
            conversation = existingConversation.Conversation;
            conversationHistory = existingConversation.History;
        }

        var processing = new StartedMessageProcessing(
            organization.Id,
            member.Id,
            conversation.Id,
            userMessage.Id,
            userMessage.Content)
        {
            ConversationHistory = conversationHistory,
            CreatedConversation = conversationId is null ? MapSummary(conversation) : null
        };

        // Existing conversations are persisted directly as InProgress by the optimized
        // repository operation. New conversations keep the legacy two-step lifecycle.
        if (conversationId is null)
        {
            await MarkAsInProgressAsync(processing, cancellationToken);
        }

        return processing;
    }

    private static ConversationSummaryResponse MapSummary(Conversation conversation) =>
        new(
            conversation.Id,
            conversation.Title,
            conversation.Status.ToString(),
            conversation.Version,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            LastMessagePreview: null);

    private async Task<Conversation> CreateConversationWithFirstMessageAsync(
        Organization organization,
        OrganizationMember member,
        Message userMessage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var conversation = CreateConversation(organization.Id, member.Id, userMessage.Content, now);
        userMessage.ConversationId = conversation.Id;

        await conversationRepository.CreateConversationWithFirstMessageAsync(
            organization.Id,
            member.Id,
            conversation,
            userMessage,
            cancellationToken);

        return conversation;
    }

    private async Task<(Conversation Conversation, IReadOnlyCollection<AiConversationMessage> History)>
        AddMessageToExistingConversationAsync(
            Guid conversationId,
            Organization organization,
            OrganizationMember member,
            Message userMessage,
            CancellationToken cancellationToken)
    {
        userMessage.ConversationId = conversationId;

        var started = await conversationRepository.StartExistingConversationMessageAsync(
            organization.Id,
            member.Id,
            conversationId,
            userMessage,
            cancellationToken)
            ?? throw CreateConversationNotFoundException();

        if (started.Conversation.Status == ConversationStatus.Archived)
        {
            throw new ConflictException(
                "The conversation is archived and cannot receive new messages.",
                ConflictException.ConversationArchived);
        }

        return (
            started.Conversation,
            started.History
                .Select(message => new AiConversationMessage(
                    message.Role == MessageRole.User
                        ? AiConversationRole.User
                        : AiConversationRole.Assistant,
                    message.Content))
                .ToArray());
    }

    public async Task MarkAsInProgressAsync(
        StartedMessageProcessing processing,
        CancellationToken cancellationToken)
    {
        var updated = await conversationRepository.UpdateMessageProcessingStatusAsync(
            processing.OrganizationId,
            processing.OwnerMemberId,
            processing.ConversationId,
            processing.UserMessageId,
            MessageProcessingStatus.InProgress,
            timeProvider.GetUtcNow(),
            cancellationToken);

        if (!updated)
        {
            throw CreateConversationNotFoundException();
        }
    }

    public async Task<CompletedMessageProcessing> CompleteAsync(
        StartedMessageProcessing processing,
        AgentTurnResult result,
        CancellationToken cancellationToken)
    {
        var completedAt = timeProvider.GetUtcNow();
        var assistantMessage = CreateAssistantMessage(result, completedAt);
        var sources = CreateSources(result.Citations);
        var warnings = CreateWarnings(result.Warnings);

        var completedMessage = await conversationRepository
            .CompleteMessageWithAssistantResponseAsync(
                processing.OrganizationId,
                processing.OwnerMemberId,
                processing.ConversationId,
                processing.UserMessageId,
                assistantMessage,
                sources,
                warnings,
                completedAt,
                cancellationToken)
            ?? throw CreateConversationNotFoundException();

        return new CompletedMessageProcessing(
            completedMessage.Id,
            completedMessage.CreatedAt);
    }

    public async Task FailAsync(
        StartedMessageProcessing processing,
        MessageProcessingFailure failure,
        CancellationToken cancellationToken)
    {
        var failureStatus = failure.WasCancelled
            ? MessageProcessingStatus.Cancelled
            : MessageProcessingStatus.Failed;
        var errorCode = ValidateErrorCode(failure.ErrorCode);

        var updated = await conversationRepository.FailMessageProcessingAsync(
            processing.OrganizationId,
            processing.OwnerMemberId,
            processing.ConversationId,
            processing.UserMessageId,
            failureStatus,
            errorCode,
            timeProvider.GetUtcNow(),
            cancellationToken);

        if (!updated)
        {
            throw CreateConversationNotFoundException();
        }
    }

    private static Conversation CreateConversation(
        Guid organizationId,
        Guid ownerMemberId,
        string firstMessage,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerMemberId = ownerMemberId,
            Title = ConversationTitleFactory.CreateFromFirstMessage(firstMessage),
            Status = ConversationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static Message CreateUserMessage(
        string message,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            Role = MessageRole.User,
            Content = message,
            ProcessingStatus = MessageProcessingStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static Message CreateAssistantMessage(
        AgentTurnResult result,
        DateTimeOffset completedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            Role = MessageRole.Assistant,
            Content = result.Content,
            Model = result.ModelName,
            ProcessingStatus = MessageProcessingStatus.Completed,
            CreatedAt = completedAt,
            UpdatedAt = completedAt
        };

    private static IReadOnlyCollection<MessageSource> CreateSources(
        IReadOnlyCollection<RetrievedEvidence> citations) =>
        citations
            .Select(evidence => new MessageSource
            {
                Id = Guid.NewGuid(),
                SourceType = evidence.SourceType,
                Title = evidence.Title,
                Url = evidence.Url,
                Reference = evidence.Reference,
                SourceDate = evidence.SourceDate
            })
            .ToArray();

    private static IReadOnlyCollection<MessageWarning> CreateWarnings(
        IReadOnlyCollection<string> warnings) =>
        warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => new MessageWarning
            {
                Id = Guid.NewGuid(),
                Code = warning,
                CreatedAt = DateTimeOffset.UtcNow
            })
            .ToArray();

    private static void EnsureMemberBelongsToOrganization(
        Organization organization,
        OrganizationMember member)
    {
        if (member.OrganizationId != organization.Id)
        {
            throw new InvalidOperationException(
                "The authenticated member does not belong to the current organization.");
        }
    }

    private static string ValidateErrorCode(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return errorCode.Length <= MaximumProcessingErrorCodeLength
            ? errorCode
            : errorCode[..MaximumProcessingErrorCodeLength];
    }

    private static NotFoundException CreateConversationNotFoundException() =>
        new("Conversation not found.", NotFoundException.ConversationNotFound);
}
