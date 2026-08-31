using AssistantCore.Repository.Abstractions;
using AssistantCore.Service.Application.Commands.SendMessage;
using AssistantCore.Service.Application.Commands.SendMessage.Models;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.Authorization;
using AssistantCore.Service.Application.Services.Messages.Lifecycle;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Responses;
using AssistantCore.Service.Application.Services.Messages.Streaming;
using AssistantCore.Service.Application.Services.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Validation;

namespace AssistantCore.Service.Tests.Messages;

public sealed class SendMessageCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AValidCommand_When_HandleAsync_Then_OrchestratesInRequiredOrder(
        SendMessageCommand command,
        MessageUserContext userContext,
        SelectedAiModel selectedModel,
        StartedMessageProcessing processing,
        AiConversationMessage historyMessage,
        MessageOrchestrationResult orchestrationResult,
        CompletedMessageProcessing completedProcessing,
        SendMessageResponse expectedResponse)
    {
        // Given
        processing = processing with
        {
            ConversationHistory = [historyMessage]
        };
        var operations = new List<string>();
        var lifecycle = new StubLifecycleService(
            operations,
            processing,
            completedProcessing);
        var orchestrator = new StubOrchestrator(operations, orchestrationResult);
        var handler = new SendMessageCommandHandler(
            new StubCommandValidator(operations),
            new StubUserContextService(operations, userContext),
            new StubModelSelector(operations, selectedModel),
            lifecycle,
            new StubToolRegistry(operations),
            orchestrator,
            new StubResponseFactory(operations, expectedResponse));

        // When
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Then
        Assert.Same(expectedResponse, result);
        Assert.Equal(
            [
                "Validate",
                "ResolveUser",
                "SelectModel",
                "StartProcessing",
                "LoadTools",
                "Orchestrate",
                "CompleteProcessing",
                "BuildResponse"
            ],
            operations);
        Assert.Equal([historyMessage], orchestrator.ReceivedConversationHistory);
        Assert.Equal(userContext.Organization.Id, orchestrator.ReceivedExecutionContext!.OrganizationId);
        Assert.Equal(userContext.Member.Id, orchestrator.ReceivedExecutionContext.MemberId);
        Assert.Equal(userContext.Organization.ExternalTenantId, orchestrator.ReceivedExecutionContext.ExternalTenantId);
        Assert.Same(processing, lifecycle.ReceivedCompletionProcessing);
        Assert.Same(orchestrationResult, lifecycle.ReceivedOrchestrationResult);
    }

    [Theory, AutoDomainData]
    public async Task Given_AConversationHistory_When_HandleAsyncStreaming_Then_PassesItToTheOrchestrator(
        SendMessageCommand command,
        MessageUserContext userContext,
        SelectedAiModel selectedModel,
        StartedMessageProcessing processing,
        AiConversationMessage historyMessage,
        MessageOrchestrationResult orchestrationResult,
        CompletedMessageProcessing completedProcessing,
        SendMessageResponse response)
    {
        // Given
        processing = processing with { ConversationHistory = [historyMessage] };
        var operations = new List<string>();
        var orchestrator = new StubOrchestrator(operations, orchestrationResult);
        var handler = new SendMessageStreamCommandHandler(
            new StubCommandValidator(operations),
            new StubUserContextService(operations, userContext),
            new StubModelSelector(operations, selectedModel),
            new StubLifecycleService(operations, processing, completedProcessing),
            new StubToolRegistry(operations),
            orchestrator,
            new StubResponseFactory(operations, response),
            new StubMessageStreamErrorReporter());

        // When
        var events = await handler.HandleAsync(
            new SendMessageStreamCommand(command.ConversationId, command.Message, command.Model),
            CancellationToken.None);
        await foreach (var _ in events)
        {
        }

        // Then
        Assert.Equal([historyMessage], orchestrator.ReceivedConversationHistory);
        Assert.Equal(userContext.Organization.Id, orchestrator.ReceivedExecutionContext!.OrganizationId);
        Assert.Equal(userContext.Member.Id, orchestrator.ReceivedExecutionContext.MemberId);
        Assert.Equal(
            userContext.Organization.ExternalTenantId,
            orchestrator.ReceivedExecutionContext.ExternalTenantId);
    }

    [Theory, AutoDomainData]
    public async Task Given_AProgressUpdate_When_HandleAsyncStreaming_Then_ReturnsADedicatedProgressEvent(
        SendMessageCommand command,
        MessageUserContext userContext,
        SelectedAiModel selectedModel,
        StartedMessageProcessing processing,
        MessageOrchestrationResult orchestrationResult,
        CompletedMessageProcessing completedProcessing,
        SendMessageResponse response)
    {
        // Given
        const string progressMessage = "Je consulte les documents pertinents.";
        var operations = new List<string>();
        var handler = new SendMessageStreamCommandHandler(
            new StubCommandValidator(operations),
            new StubUserContextService(operations, userContext),
            new StubModelSelector(operations, selectedModel),
            new StubLifecycleService(operations, processing, completedProcessing),
            new StubToolRegistry(operations),
            new StubOrchestrator(operations, orchestrationResult, progressMessage: progressMessage),
            new StubResponseFactory(operations, response),
            new StubMessageStreamErrorReporter());

        // When
        var events = await handler.HandleAsync(
            new SendMessageStreamCommand(command.ConversationId, command.Message, command.Model),
            CancellationToken.None);
        var receivedEvents = new List<SendMessageStreamEvent>();
        await foreach (var streamEvent in events)
        {
            receivedEvents.Add(streamEvent);
        }

        // Then
        var progressEvent = Assert.Single(receivedEvents, streamEvent =>
            streamEvent.Name == SendMessageStreamEvent.ProgressUpdated);
        Assert.Equal(
            progressMessage,
            progressEvent.Data.GetType().GetProperty("Message")?.GetValue(progressEvent.Data));
    }

    [Theory, AutoDomainData]
    public async Task Given_AProviderTimeout_When_HandleAsyncStreaming_Then_ReturnsTheTimeoutErrorCode(
        SendMessageCommand command,
        MessageUserContext userContext,
        SelectedAiModel selectedModel,
        StartedMessageProcessing processing,
        MessageOrchestrationResult orchestrationResult,
        CompletedMessageProcessing completedProcessing,
        SendMessageResponse response)
    {
        // Given
        var operations = new List<string>();
        var lifecycle = new StubLifecycleService(operations, processing, completedProcessing);
        var errorReporter = new StubMessageStreamErrorReporter();
        var handler = new SendMessageStreamCommandHandler(
            new StubCommandValidator(operations),
            new StubUserContextService(operations, userContext),
            new StubModelSelector(operations, selectedModel),
            lifecycle,
            new StubToolRegistry(operations),
            new StubOrchestrator(
                operations,
                orchestrationResult,
                new AiProviderTimeoutException(selectedModel.Provider)),
            new StubResponseFactory(operations, response),
            errorReporter);

        // When
        var events = await handler.HandleAsync(
            new SendMessageStreamCommand(command.ConversationId, command.Message, command.Model),
            CancellationToken.None);
        var receivedEvents = new List<SendMessageStreamEvent>();
        await foreach (var streamEvent in events)
        {
            receivedEvents.Add(streamEvent);
        }

        // Then
        var errorEvent = Assert.Single(receivedEvents, streamEvent =>
            streamEvent.Name == SendMessageStreamEvent.Error);
        Assert.Equal(
            "ai_provider_timeout",
            errorEvent.Data.GetType().GetProperty("Code")?.GetValue(errorEvent.Data));
        Assert.False(lifecycle.ReceivedFailure?.WasCancelled);
        Assert.IsType<AiProviderTimeoutException>(errorReporter.ReceivedException);
        Assert.Equal("ai_provider_timeout", errorReporter.ReceivedErrorCode);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInvalidCommand_When_HandleAsync_Then_StopsBeforeResolvingUser(
        SendMessageCommand command,
        MessageUserContext userContext,
        SelectedAiModel selectedModel,
        StartedMessageProcessing processing,
        MessageOrchestrationResult orchestrationResult,
        CompletedMessageProcessing completedProcessing,
        SendMessageResponse response)
    {
        // Given
        var operations = new List<string>();
        var expectedException = new BadRequestException("Invalid message.");
        var handler = CreateHandler(
            operations,
            userContext,
            selectedModel,
            processing,
            orchestrationResult,
            completedProcessing,
            response,
            validationException: expectedException);

        // When
        var exception = await Record.ExceptionAsync(() =>
            handler.HandleAsync(command, CancellationToken.None));

        // Then
        Assert.Same(expectedException, exception);
        Assert.Equal(["Validate"], operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_AForbiddenUserContext_When_HandleAsync_Then_StopsBeforeSelectingModel(
        SendMessageCommand command,
        MessageUserContext userContext,
        SelectedAiModel selectedModel,
        StartedMessageProcessing processing,
        MessageOrchestrationResult orchestrationResult,
        CompletedMessageProcessing completedProcessing,
        SendMessageResponse response)
    {
        // Given
        var operations = new List<string>();
        var expectedException = new ForbiddenException("Organization access denied.");
        var handler = CreateHandler(
            operations,
            userContext,
            selectedModel,
            processing,
            orchestrationResult,
            completedProcessing,
            response,
            authorizationException: expectedException);

        // When
        var exception = await Record.ExceptionAsync(() =>
            handler.HandleAsync(command, CancellationToken.None));

        // Then
        Assert.Same(expectedException, exception);
        Assert.Equal(["Validate", "ResolveUser"], operations);
    }

    private static SendMessageCommandHandler CreateHandler(
        List<string> operations,
        MessageUserContext userContext,
        SelectedAiModel selectedModel,
        StartedMessageProcessing processing,
        MessageOrchestrationResult orchestrationResult,
        CompletedMessageProcessing completedProcessing,
        SendMessageResponse response,
        Exception? validationException = null,
        Exception? authorizationException = null) =>
        new(
            new StubCommandValidator(operations, validationException),
            new StubUserContextService(operations, userContext, authorizationException),
            new StubModelSelector(operations, selectedModel),
            new StubLifecycleService(operations, processing, completedProcessing),
            new StubToolRegistry(operations),
            new StubOrchestrator(operations, orchestrationResult),
            new StubResponseFactory(operations, response));

    private sealed class StubCommandValidator(
        List<string> operations,
        Exception? exception = null) : ISendMessageCommandValidator
    {
        public Task<SendMessageCommand> ValidateAsync(
            SendMessageCommand command,
            CancellationToken cancellationToken)
        {
            operations.Add("Validate");
            return exception is null
                ? Task.FromResult(command)
                : Task.FromException<SendMessageCommand>(exception);
        }
    }

    private sealed class StubUserContextService(
        List<string> operations,
        MessageUserContext context,
        Exception? exception = null) : IMessageUserContextService
    {
        public Task<MessageUserContext> GetCurrentAsync(CancellationToken cancellationToken)
        {
            operations.Add("ResolveUser");
            return exception is null
                ? Task.FromResult(context)
                : Task.FromException<MessageUserContext>(exception);
        }
    }

    private sealed class StubModelSelector(
        List<string> operations,
        SelectedAiModel selectedModel) : IAuthorizedAiModelSelector
    {
        public bool IsAvailable(string? requestedModel) => true;

        public Task<SelectedAiModel> SelectAsync(
            Guid organizationId,
            string? requestedModel,
            CancellationToken cancellationToken)
        {
            operations.Add("SelectModel");
            return Task.FromResult(selectedModel);
        }
    }

    private sealed class StubLifecycleService(
        List<string> operations,
        StartedMessageProcessing processing,
        CompletedMessageProcessing completedProcessing)
        : IMessageProcessingLifecycleService
    {
        public StartedMessageProcessing? ReceivedCompletionProcessing { get; private set; }

        public MessageOrchestrationResult? ReceivedOrchestrationResult { get; private set; }

        public MessageProcessingFailure? ReceivedFailure { get; private set; }

        public Task<StartedMessageProcessing> StartAsync(
            Guid? conversationId,
            string message,
            AssistantCore.Repository.Domain.Entities.Organization organization,
            AssistantCore.Repository.Domain.Entities.OrganizationMember member,
            CancellationToken cancellationToken)
        {
            operations.Add("StartProcessing");
            return Task.FromResult(processing);
        }

        public Task<CompletedMessageProcessing> CompleteAsync(
            StartedMessageProcessing startedProcessing,
            MessageOrchestrationResult result,
            CancellationToken cancellationToken)
        {
            operations.Add("CompleteProcessing");
            ReceivedCompletionProcessing = startedProcessing;
            ReceivedOrchestrationResult = result;
            return Task.FromResult(completedProcessing);
        }

        public Task MarkAsInProgressAsync(
            StartedMessageProcessing startedProcessing,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task FailAsync(
            StartedMessageProcessing startedProcessing,
            MessageProcessingFailure failure,
            CancellationToken cancellationToken)
        {
            ReceivedFailure = failure;
            return Task.CompletedTask;
        }
    }

    private sealed class StubToolRegistry(List<string> operations) : IAiToolRegistry
    {
        public Task<IReadOnlyCollection<AiToolDefinition>> GetAvailableToolsAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
        {
            operations.Add("LoadTools");
            return Task.FromResult<IReadOnlyCollection<AiToolDefinition>>([]);
        }
    }

    private sealed class StubOrchestrator(
        List<string> operations,
        MessageOrchestrationResult result,
        Exception? exception = null,
        string? progressMessage = null) : IMessageToolOrchestrator
    {
        public IReadOnlyCollection<AiConversationMessage> ReceivedConversationHistory { get; private set; }
            = [];

        public ConnectorExecutionContext? ReceivedExecutionContext { get; private set; }

        public Task<MessageOrchestrationResult> OrchestrateAsync(
            StartedMessageProcessing processing,
            ConnectorExecutionContext executionContext,
            SelectedAiModel selectedModel,
            IReadOnlyCollection<AiConversationMessage> conversationHistory,
            IReadOnlyCollection<AiToolDefinition> availableTools,
            CancellationToken cancellationToken)
        {
            operations.Add("Orchestrate");
            ReceivedExecutionContext = executionContext;
            ReceivedConversationHistory = conversationHistory;
            return exception is null
                ? Task.FromResult(result)
                : Task.FromException<MessageOrchestrationResult>(exception);
        }

        public async Task<MessageOrchestrationResult> OrchestrateStreamingAsync(
            StartedMessageProcessing processing,
            ConnectorExecutionContext executionContext,
            SelectedAiModel selectedModel,
            IReadOnlyCollection<AiConversationMessage> conversationHistory,
            IReadOnlyCollection<AiToolDefinition> availableTools,
            Func<string, CancellationToken, ValueTask> onProgress,
            Func<string, CancellationToken, ValueTask> onAnswerDelta,
            CancellationToken cancellationToken)
        {
            if (progressMessage is not null)
            {
                await onProgress(progressMessage, cancellationToken);
            }

            return await OrchestrateAsync(
                processing,
                executionContext,
                selectedModel,
                conversationHistory,
                availableTools,
                cancellationToken);
        }
    }

    private sealed class StubMessageStreamErrorReporter : IMessageStreamErrorReporter
    {
        public Exception? ReceivedException { get; private set; }

        public string? ReceivedErrorCode { get; private set; }

        public void Report(
            Exception exception,
            Guid? conversationId,
            Guid? userMessageId,
            string errorCode)
        {
            ReceivedException = exception;
            ReceivedErrorCode = errorCode;
        }
    }

    private sealed class StubResponseFactory(
        List<string> operations,
        SendMessageResponse response) : ISendMessageResponseFactory
    {
        public SendMessageResponse Create(
            StartedMessageProcessing processing,
            MessageOrchestrationResult orchestrationResult,
            CompletedMessageProcessing completedProcessing)
        {
            operations.Add("BuildResponse");
            return response;
        }
    }
}
