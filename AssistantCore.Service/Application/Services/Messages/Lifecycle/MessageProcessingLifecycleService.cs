using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Conversations;
using AssistantCore.Service.Application.Services.Messages.Memory;

namespace AssistantCore.Service.Application.Services.Messages.Lifecycle;

public sealed class MessageProcessingLifecycleService(
    IConversationRepository conversationRepository,
    IConversationMemorySummaryService conversationMemorySummaryService,
    TimeProvider timeProvider) : IMessageProcessingLifecycleService
{
    private const int MaximumProcessingErrorCodeLength = 100;

    public MessageProcessingLifecycleService(
        IConversationRepository conversationRepository,
        TimeProvider timeProvider)
        : this(
            conversationRepository,
            new DeterministicConversationMemorySummaryService(),
            timeProvider)
    {
    }

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

        await MarkAsInProgressAsync(processing, cancellationToken);

        return processing;
    }

    /// <summary>
    /// Projette la conversation vers le resume que la liste retourne, afin qu'un
    /// client puisse l'inserer directement sans convertir une forme parallele.
    /// L'apercu reste null : au moment ou la conversation est creee, la reponse de
    /// l'Assistant n'existe pas encore et deviendra le dernier message quelques
    /// instants plus tard.
    /// </summary>
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
        var conversation = await conversationRepository.FindConversationAsync(
            organization.Id,
            member.Id,
            conversationId,
            cancellationToken)
            ?? throw CreateConversationNotFoundException();

        if (conversation.Status == ConversationStatus.Archived)
        {
            throw new ConflictException(
                "The conversation is archived and cannot receive new messages.",
                ConflictException.ConversationArchived);
        }

        var history = await conversationRepository.GetConversationHistoryAsync(
            organization.Id,
            member.Id,
            conversationId,
            cancellationToken);

        userMessage.ConversationId = conversation.Id;
        var addedMessage = await conversationRepository.AddUserMessageAsync(
            organization.Id,
            member.Id,
            conversation.Id,
            userMessage,
            cancellationToken);

        if (addedMessage is null)
        {
            throw CreateConversationNotFoundException();
        }

        return (
            conversation,
            history
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
        MessageOrchestrationResult result,
        CancellationToken cancellationToken)
    {
        var completedAt = timeProvider.GetUtcNow();
        var assistantMessage = CreateAssistantMessage(result, completedAt);
        var sources = CreateSources(result.CitedEvidence);
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

        var summary = processing.SelectedModel is { } selectedModel
            ? await conversationMemorySummaryService.CreateAsync(
                selectedModel,
                processing.ConversationHistory,
                processing.UserMessage,
                result.Answer,
                cancellationToken)
            : null;
        summary ??= CreateContextSummaryEntry(processing.UserMessage, result.Answer);

        await conversationRepository.UpdateConversationContextSummaryAsync(
            processing.OrganizationId,
            processing.OwnerMemberId,
            processing.ConversationId,
            summary,
            completedAt,
            cancellationToken);

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
        MessageOrchestrationResult result,
        DateTimeOffset completedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            Role = MessageRole.Assistant,
            Content = result.Answer,
            ProcessingStatus = MessageProcessingStatus.Completed,
            Model = result.ModelName,
            CreatedAt = completedAt,
            UpdatedAt = completedAt
        };

    private static IReadOnlyCollection<MessageSource> CreateSources(
        IReadOnlyCollection<RetrievedEvidence> evidence) =>
        evidence
            .Select(item => new MessageSource
            {
                Id = Guid.NewGuid(),
                SourceType = item.SourceType,
                Title = item.Title,
                Reference = item.Reference,
                Url = item.Url,
                SourceDate = item.OccurredAt
            })
            .ToArray();

    private static IReadOnlyCollection<MessageWarning> CreateWarnings(
        IReadOnlyCollection<string> warnings) =>
        warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => new MessageWarning
            {
                Id = Guid.NewGuid(),
                Content = warning.Trim()
            })
            .ToArray();

    private static string CreateContextSummaryEntry(string userMessage, string answer) =>
        $"User: {userMessage}\nAssistant: {answer}";

    private sealed class DeterministicConversationMemorySummaryService
        : IConversationMemorySummaryService
    {
        public Task<string?> CreateAsync(
            SelectedAiModel model,
            IReadOnlyCollection<AiConversationMessage> conversationHistory,
            string currentUserMessage,
            string currentAssistantMessage,
            CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private static string ValidateErrorCode(string errorCode)
    {
        var normalizedErrorCode = errorCode.Trim();

        if (normalizedErrorCode.Length is 0 or > MaximumProcessingErrorCodeLength)
        {
            throw new ArgumentException(
                $"The error code must contain between 1 and {MaximumProcessingErrorCodeLength} characters.",
                nameof(errorCode));
        }

        return normalizedErrorCode;
    }

    private static void EnsureMemberBelongsToOrganization(
        Organization organization,
        OrganizationMember member)
    {
        if (member.OrganizationId != organization.Id)
        {
            throw new ArgumentException(
                "The organization member does not belong to the provided organization.",
                nameof(member));
        }
    }

    private static NotFoundException CreateConversationNotFoundException() =>
        new("Conversation not found.");
}
