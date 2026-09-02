using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Lifecycle;
using AssistantCore.Service.Application.Services.Messages.Memory;

namespace AssistantCore.Service.Tests.Messages;

public sealed class MessageProcessingLifecycleServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_NoConversationIdentifier_When_StartAsync_Then_CreatesConversationAndStartsMessage(
        Organization organization,
        OrganizationMember member,
        DateTimeOffset now)
    {
        // Given
        member.OrganizationId = organization.Id;
        var repository = new RecordingConversationRepository();
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(now));
        using var cancellationTokenSource = new CancellationTokenSource();

        // When
        var result = await service.StartAsync(
            null,
            "Question already validated",
            organization,
            member,
            cancellationTokenSource.Token);

        // Then
        Assert.NotNull(repository.CreatedConversation);
        Assert.NotNull(repository.CreatedFirstMessage);
        Assert.Equal(organization.Id, repository.CreatedConversation.OrganizationId);
        Assert.Equal(member.Id, repository.CreatedConversation.OwnerMemberId);
        Assert.Equal(ConversationStatus.Active, repository.CreatedConversation.Status);
        Assert.Equal("Question already validated", repository.CreatedConversation.Title);
        Assert.Equal(now, repository.CreatedConversation.CreatedAt);
        Assert.Equal(now, repository.CreatedConversation.UpdatedAt);
        Assert.Equal(repository.CreatedConversation.Id, repository.CreatedFirstMessage.ConversationId);
        Assert.Equal(MessageRole.User, repository.CreatedFirstMessage.Role);
        Assert.Equal(MessageProcessingStatus.Pending, repository.CreatedFirstMessage.ProcessingStatus);
        Assert.Equal("Question already validated", repository.CreatedFirstMessage.Content);
        Assert.Equal(MessageProcessingStatus.InProgress, repository.ReceivedProcessingStatus);
        Assert.Equal(repository.CreatedFirstMessage.Id, repository.ReceivedMessageId);
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(member.Id, result.OwnerMemberId);
        Assert.Equal(repository.CreatedConversation.Id, result.ConversationId);
        Assert.Equal(repository.CreatedFirstMessage.Id, result.UserMessageId);
        Assert.Equal(["CreateConversation", "UpdateStatus"], repository.Operations);
        Assert.Equal(cancellationTokenSource.Token, repository.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoConversationIdentifier_When_StartAsync_Then_DescribesTheCreatedConversation(
        Organization organization,
        OrganizationMember member,
        DateTimeOffset now)
    {
        // Given
        member.OrganizationId = organization.Id;
        var repository = new RecordingConversationRepository();
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(now));

        // When
        var result = await service.StartAsync(
            null,
            "Politique de teletravail",
            organization,
            member,
            CancellationToken.None);

        // Then
        Assert.NotNull(repository.CreatedConversation);
        Assert.NotNull(result.CreatedConversation);
        Assert.Equal(repository.CreatedConversation.Id, result.CreatedConversation.Id);
        Assert.Equal("Politique de teletravail", result.CreatedConversation.Title);
        Assert.Equal(nameof(ConversationStatus.Active), result.CreatedConversation.Status);
        Assert.Equal(1, result.CreatedConversation.Version);
        Assert.Equal(now, result.CreatedConversation.CreatedAt);
        Assert.Equal(now, result.CreatedConversation.UpdatedAt);
        Assert.Null(result.CreatedConversation.LastMessagePreview);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnOwnedConversation_When_StartAsync_Then_DoesNotDescribeTheConversation(
        Organization organization,
        OrganizationMember member,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        member.OrganizationId = organization.Id;
        conversation.OrganizationId = organization.Id;
        conversation.OwnerMemberId = member.Id;
        var repository = new RecordingConversationRepository
        {
            FoundConversation = conversation
        };
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(now));

        // When
        var result = await service.StartAsync(
            conversation.Id,
            "Another validated question",
            organization,
            member,
            CancellationToken.None);

        // Then
        Assert.Null(result.CreatedConversation);
    }

    [Theory, AutoDomainData]
    public async Task Given_ALongFirstMessage_When_StartAsync_Then_TruncatesTheDerivedTitle(
        Organization organization,
        OrganizationMember member,
        DateTimeOffset now)
    {
        // Given
        member.OrganizationId = organization.Id;
        var repository = new RecordingConversationRepository();
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(now));
        var longMessage = new string('a', 250);

        // When
        await service.StartAsync(
            null,
            longMessage,
            organization,
            member,
            CancellationToken.None);

        // Then
        Assert.NotNull(repository.CreatedConversation);
        Assert.Equal(200, repository.CreatedConversation.Title.Length);
        Assert.EndsWith("…", repository.CreatedConversation.Title, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnOwnedConversation_When_StartAsync_Then_AddsAndStartsMessage(
        Organization organization,
        OrganizationMember member,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        member.OrganizationId = organization.Id;
        conversation.OrganizationId = organization.Id;
        conversation.OwnerMemberId = member.Id;
        var repository = new RecordingConversationRepository
        {
            FoundConversation = conversation
        };
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(now));
        using var cancellationTokenSource = new CancellationTokenSource();

        // When
        var result = await service.StartAsync(
            conversation.Id,
            "Another validated question",
            organization,
            member,
            cancellationTokenSource.Token);

        // Then
        Assert.Null(repository.CreatedConversation);
        Assert.NotNull(repository.AddedUserMessage);
        Assert.Equal(conversation.Id, repository.AddedUserMessage.ConversationId);
        Assert.Equal(MessageRole.User, repository.AddedUserMessage.Role);
        Assert.Equal(MessageProcessingStatus.Pending, repository.AddedUserMessage.ProcessingStatus);
        Assert.Equal("Another validated question", repository.AddedUserMessage.Content);
        Assert.Equal(MessageProcessingStatus.InProgress, repository.ReceivedProcessingStatus);
        Assert.Equal(repository.AddedUserMessage.Id, result.UserMessageId);
        Assert.Equal(
            ["FindConversation", "GetConversationHistory", "AddUserMessage", "UpdateStatus"],
            repository.Operations);
        Assert.Equal(cancellationTokenSource.Token, repository.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnavailableConversation_When_StartAsync_Then_ThrowsNotFoundWithoutAddingMessage(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        member.OrganizationId = organization.Id;
        var repository = new RecordingConversationRepository();
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(DateTimeOffset.UtcNow));

        // When
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.StartAsync(
                Guid.NewGuid(),
                "Question",
                organization,
                member,
                CancellationToken.None));

        // Then
        Assert.Equal("Conversation not found.", exception.Message);
        Assert.Null(repository.AddedUserMessage);
        Assert.Null(repository.ReceivedProcessingStatus);
        Assert.Equal(["FindConversation"], repository.Operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_AConversationRemovedBeforeMessageIsAdded_When_StartAsync_Then_ThrowsNotFoundWithoutStartingMessage(
        Organization organization,
        OrganizationMember member,
        Conversation conversation)
    {
        // Given
        member.OrganizationId = organization.Id;
        conversation.OrganizationId = organization.Id;
        conversation.OwnerMemberId = member.Id;
        var repository = new RecordingConversationRepository
        {
            FoundConversation = conversation,
            ReturnNullWhenAddingMessage = true
        };
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(DateTimeOffset.UtcNow));

        // When
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.StartAsync(
                conversation.Id,
                "Question",
                organization,
                member,
                CancellationToken.None));

        // Then
        Assert.Equal("Conversation not found.", exception.Message);
        Assert.Null(repository.ReceivedProcessingStatus);
        Assert.Equal(
            ["FindConversation", "GetConversationHistory", "AddUserMessage"],
            repository.Operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_AMemberFromAnotherOrganization_When_StartAsync_Then_ThrowsBeforePersistence(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        member.OrganizationId = Guid.NewGuid();
        var repository = new RecordingConversationRepository();
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(DateTimeOffset.UtcNow));

        // When
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.StartAsync(
                null,
                "Question",
                organization,
                member,
                CancellationToken.None));

        // Then
        Assert.Contains("does not belong", exception.Message, StringComparison.Ordinal);
        Assert.Empty(repository.Operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_AValidOrchestrationResult_When_CompleteAsync_Then_PersistsAssistantResponseSourcesAndWarnings(
        StartedMessageProcessing processing,
        DateTimeOffset completedAt)
    {
        // Given
        var evidence = new RetrievedEvidence(
            "evidence-1",
            "ERP",
            "Montreal inventory",
            "248 units available",
            "inventory-item-1",
            null,
            completedAt.AddMinutes(-1));
        var orchestrationResult = new MessageOrchestrationResult(
            "There are 248 units available.",
            "gpt",
            [evidence],
            ["Quebec inventory was unavailable."],
            new OrchestrationExecutionUsage(
                TimeSpan.FromSeconds(2),
                InputTokens: 100,
                OutputTokens: 20,
                ModelCallCount: 2,
                ToolCallCount: 1,
                EstimatedCost: 0.01m,
                ContextSize: 100,
                RepeatedToolCallCount: 0));
        var repository = new RecordingConversationRepository();
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(completedAt));
        using var cancellationTokenSource = new CancellationTokenSource();

        // When
        var result = await service.CompleteAsync(
            processing,
            orchestrationResult,
            cancellationTokenSource.Token);

        // Then
        Assert.NotNull(repository.CompletedAssistantMessage);
        Assert.Equal(MessageRole.Assistant, repository.CompletedAssistantMessage.Role);
        Assert.Equal(MessageProcessingStatus.Completed, repository.CompletedAssistantMessage.ProcessingStatus);
        Assert.Equal(orchestrationResult.Answer, repository.CompletedAssistantMessage.Content);
        Assert.Equal(orchestrationResult.ModelName, repository.CompletedAssistantMessage.Model);
        Assert.Equal(completedAt, repository.CompletedAssistantMessage.CreatedAt);
        var persistedSource = Assert.Single(repository.CompletedSources);
        Assert.Equal(evidence.SourceType, persistedSource.SourceType);
        Assert.Equal(evidence.Title, persistedSource.Title);
        Assert.Equal(evidence.Reference, persistedSource.Reference);
        Assert.Equal(evidence.OccurredAt, persistedSource.SourceDate);
        var persistedWarning = Assert.Single(repository.CompletedWarnings);
        Assert.Equal("Quebec inventory was unavailable.", persistedWarning.Content);
        Assert.Equal(repository.CompletedAssistantMessage.Id, result.AssistantMessageId);
        Assert.Equal(completedAt, result.CreatedAt);
        Assert.Equal(cancellationTokenSource.Token, repository.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_ANonEmptyAiMemory_When_CompleteAsync_Then_PersistsTheAiGeneratedSummary(
        StartedMessageProcessing processing,
        MessageOrchestrationResult orchestrationResult,
        DateTimeOffset completedAt)
    {
        // Given
        processing.SelectedModel = new SelectedAiModel("OpenAI", "gpt-5-mini");
        var repository = new RecordingConversationRepository();
        var summaryService = new StubConversationMemorySummaryService("Facts\n- The team selected blue.");
        var service = new MessageProcessingLifecycleService(
            repository,
            summaryService,
            new StubTimeProvider(completedAt));

        // When
        await service.CompleteAsync(processing, orchestrationResult, CancellationToken.None);

        // Then
        Assert.Equal("Facts\n- The team selected blue.", repository.ReceivedContextSummary);
        Assert.Equal(processing.SelectedModel, summaryService.ReceivedModel);
        Assert.Equal(processing.UserMessage, summaryService.ReceivedUserMessage);
        Assert.Equal(orchestrationResult.Answer, summaryService.ReceivedAssistantMessage);
    }

    [Theory, AutoDomainData]
    public async Task Given_RepositoryRejectsCompletion_When_CompleteAsync_Then_ThrowsNotFound(
        StartedMessageProcessing processing,
        MessageOrchestrationResult orchestrationResult,
        DateTimeOffset completedAt)
    {
        // Given
        var repository = new RecordingConversationRepository
        {
            ReturnNullWhenCompletingMessage = true
        };
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(completedAt));

        // When
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CompleteAsync(
                processing,
                orchestrationResult,
                CancellationToken.None));

        // Then
        Assert.Equal("Conversation not found.", exception.Message);
    }

    [Theory]
    [InlineAutoDomainData(false, MessageProcessingStatus.Failed)]
    [InlineAutoDomainData(true, MessageProcessingStatus.Cancelled)]
    public async Task Given_AProcessingFailure_When_FailAsync_Then_PersistsTheExpectedTerminalStatus(
        bool wasCancelled,
        MessageProcessingStatus expectedStatus,
        StartedMessageProcessing processing,
        DateTimeOffset failedAt)
    {
        // Given
        var repository = new RecordingConversationRepository();
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(failedAt));
        using var cancellationTokenSource = new CancellationTokenSource();

        // When
        await service.FailAsync(
            processing,
            new MessageProcessingFailure("  provider_unavailable  ", wasCancelled),
            cancellationTokenSource.Token);

        // Then
        Assert.Equal(expectedStatus, repository.ReceivedFailureStatus);
        Assert.Equal("provider_unavailable", repository.ReceivedErrorCode);
        Assert.Equal(failedAt, repository.ReceivedFailureDate);
        Assert.Equal(cancellationTokenSource.Token, repository.ReceivedCancellationToken);
        Assert.Equal(["FailMessage"], repository.Operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_RepositoryRejectsFailure_When_FailAsync_Then_ThrowsNotFound(
        StartedMessageProcessing processing)
    {
        // Given
        var repository = new RecordingConversationRepository
        {
            ReturnFalseWhenFailingMessage = true
        };
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(DateTimeOffset.UtcNow));

        // When
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.FailAsync(
                processing,
                new MessageProcessingFailure("provider_unavailable", false),
                CancellationToken.None));

        // Then
        Assert.Equal("Conversation not found.", exception.Message);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInvalidErrorCode_When_FailAsync_Then_ThrowsBeforePersistence(
        StartedMessageProcessing processing)
    {
        // Given
        var repository = new RecordingConversationRepository();
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(DateTimeOffset.UtcNow));

        // When
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.FailAsync(
                processing,
                new MessageProcessingFailure(new string('x', 101), false),
                CancellationToken.None));

        // Then
        Assert.Equal("errorCode", exception.ParamName);
        Assert.Empty(repository.Operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_RepositoryFailure_When_FailAsync_Then_PropagatesTheException(
        StartedMessageProcessing processing)
    {
        // Given
        var persistenceException = new InvalidOperationException("Persistence failed.");
        var repository = new RecordingConversationRepository
        {
            FailureException = persistenceException
        };
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(DateTimeOffset.UtcNow));

        // When
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FailAsync(
                processing,
                new MessageProcessingFailure("provider_unavailable", false),
                CancellationToken.None));

        // Then
        Assert.Same(persistenceException, exception);
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubConversationMemorySummaryService(string? summary)
        : IConversationMemorySummaryService
    {
        public SelectedAiModel? ReceivedModel { get; private set; }

        public string? ReceivedUserMessage { get; private set; }

        public string? ReceivedAssistantMessage { get; private set; }

        public Task<string?> CreateAsync(
            SelectedAiModel model,
            IReadOnlyCollection<AiConversationMessage> conversationHistory,
            string currentUserMessage,
            string currentAssistantMessage,
            CancellationToken cancellationToken)
        {
            ReceivedModel = model;
            ReceivedUserMessage = currentUserMessage;
            ReceivedAssistantMessage = currentAssistantMessage;
            return Task.FromResult(summary);
        }
    }

    [Theory, AutoDomainData]
    public async Task Given_AnArchivedConversation_When_StartAsync_Then_ThrowsAConflictWithTheArchivedCode(
        Organization organization,
        OrganizationMember member,
        Conversation conversation,
        DateTimeOffset now)
    {
        // Given
        member.OrganizationId = organization.Id;
        conversation.OrganizationId = organization.Id;
        conversation.OwnerMemberId = member.Id;
        conversation.Status = ConversationStatus.Archived;
        var repository = new RecordingConversationRepository { FoundConversation = conversation };
        var service = new MessageProcessingLifecycleService(
            repository,
            new StubTimeProvider(now));

        // When
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.StartAsync(
                conversation.Id,
                "Nouvelle question",
                organization,
                member,
                CancellationToken.None));

        // Then
        Assert.Equal(ConflictException.ConversationArchived, exception.ErrorCode);
    }

    private sealed class RecordingConversationRepository : IConversationRepository
    {
        public Task<ConversationUpdateResult> UpdateConversationAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            int? expectedVersion,
            string? title,
            ConversationStatus? status,
            DateTimeOffset updatedAt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ConversationDeleteStatus> SoftDeleteConversationAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            DateTimeOffset deletedAt,
            DateTimeOffset purgeAfter,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Conversation? FoundConversation { get; init; }

        public bool ReturnNullWhenAddingMessage { get; init; }

        public bool ReturnNullWhenCompletingMessage { get; init; }

        public bool ReturnFalseWhenFailingMessage { get; init; }

        public Exception? FailureException { get; init; }

        public Conversation? CreatedConversation { get; private set; }

        public Message? CreatedFirstMessage { get; private set; }

        public Message? AddedUserMessage { get; private set; }

        public Guid? ReceivedMessageId { get; private set; }

        public MessageProcessingStatus? ReceivedProcessingStatus { get; private set; }

        public Message? CompletedAssistantMessage { get; private set; }

        public IReadOnlyCollection<MessageSource> CompletedSources { get; private set; } = [];

        public IReadOnlyCollection<MessageWarning> CompletedWarnings { get; private set; } = [];

        public MessageProcessingStatus? ReceivedFailureStatus { get; private set; }

        public string? ReceivedErrorCode { get; private set; }

        public DateTimeOffset? ReceivedFailureDate { get; private set; }

        public string? ReceivedContextSummary { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public List<string> Operations { get; } = [];

        public Task<(Conversation Conversation, Message UserMessage)> CreateConversationWithFirstMessageAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Conversation conversation,
            Message userMessage,
            CancellationToken cancellationToken = default)
        {
            Operations.Add("CreateConversation");
            // La persistance reelle affecte la version initiale avant d'enregistrer.
            conversation.Version = 1;
            CreatedConversation = conversation;
            CreatedFirstMessage = userMessage;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult((conversation, userMessage));
        }

        public Task<Conversation?> FindConversationAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            Operations.Add("FindConversation");
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(FoundConversation);
        }

        public Task<ConversationMessagePage> ListMessagesAsync(
            Guid conversationId,
            int limit,
            DateTimeOffset? cursorCreatedAt,
            Guid? cursorId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ConversationMessageItem>> GetConversationHistoryAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            Operations.Add("GetConversationHistory");
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult<IReadOnlyList<ConversationMessageItem>>([]);
        }

        public Task<bool> UpdateConversationContextSummaryAsync(
            Guid organizationId, Guid ownerMemberId, Guid conversationId, string summary,
            DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
        {
            Operations.Add("UpdateContextSummary");
            ReceivedContextSummary = summary;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(true);
        }

        public Task<ConversationListPage> ListConversationsAsync(
            Guid organizationId,
            Guid ownerMemberId,
            ConversationStatus status,
            int limit,
            DateTimeOffset? cursorUpdatedAt,
            Guid? cursorId,
            CancellationToken cancellationToken = default)
        {
            Operations.Add("ListConversations");
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(new ConversationListPage([], false));
        }

        public Task<Message?> AddUserMessageAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            Message userMessage,
            CancellationToken cancellationToken = default)
        {
            Operations.Add("AddUserMessage");
            AddedUserMessage = userMessage;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(ReturnNullWhenAddingMessage ? null : userMessage);
        }

        public Task<bool> UpdateMessageProcessingStatusAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            Guid messageId,
            MessageProcessingStatus status,
            DateTimeOffset updatedAt,
            CancellationToken cancellationToken = default)
        {
            Operations.Add("UpdateStatus");
            ReceivedMessageId = messageId;
            ReceivedProcessingStatus = status;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(true);
        }

        public Task<Message?> CompleteMessageWithAssistantResponseAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            Guid userMessageId,
            Message assistantMessage,
            IReadOnlyCollection<MessageSource> sources,
            IReadOnlyCollection<MessageWarning> warnings,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken = default)
        {
            Operations.Add("CompleteMessage");
            CompletedAssistantMessage = assistantMessage;
            CompletedSources = sources;
            CompletedWarnings = warnings;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(ReturnNullWhenCompletingMessage ? null : assistantMessage);
        }

        public Task<bool> FailMessageProcessingAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            Guid userMessageId,
            MessageProcessingStatus failureStatus,
            string errorCode,
            DateTimeOffset failedAt,
            CancellationToken cancellationToken = default)
        {
            Operations.Add("FailMessage");
            ReceivedFailureStatus = failureStatus;
            ReceivedErrorCode = errorCode;
            ReceivedFailureDate = failedAt;
            ReceivedCancellationToken = cancellationToken;

            if (FailureException is not null)
            {
                return Task.FromException<bool>(FailureException);
            }

            return Task.FromResult(!ReturnFalseWhenFailingMessage);
        }
    }
}
