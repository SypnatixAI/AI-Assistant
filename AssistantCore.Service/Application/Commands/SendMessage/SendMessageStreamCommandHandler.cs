using System.Threading.Channels;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.SendMessage.Models;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.Authorization;
using AssistantCore.Service.Application.Services.Messages.Lifecycle;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Responses;
using AssistantCore.Service.Application.Services.Messages.Streaming;
using AssistantCore.Service.Application.Services.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Validation;

namespace AssistantCore.Service.Application.Commands.SendMessage;

public sealed class SendMessageStreamCommandHandler(
    ISendMessageCommandValidator validator,
    IMessageUserContextService userContextService,
    IAuthorizedAiModelSelector modelSelector,
    IMessageProcessingLifecycleService lifecycleService,
    IAiToolRegistry toolRegistry,
    IMessageToolOrchestrator orchestrator,
    ISendMessageResponseFactory responseFactory,
    IMessageStreamErrorReporter errorReporter)
    : IRequestHandler<SendMessageStreamCommand, IAsyncEnumerable<SendMessageStreamEvent>>
{
    public Task<IAsyncEnumerable<SendMessageStreamEvent>> HandleAsync(
        SendMessageStreamCommand request,
        CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<SendMessageStreamEvent>();
        _ = ProduceAsync(request, channel.Writer, cancellationToken);

        return Task.FromResult<IAsyncEnumerable<SendMessageStreamEvent>>(
            channel.Reader.ReadAllAsync(cancellationToken));
    }

    private async Task ProduceAsync(
        SendMessageStreamCommand request,
        ChannelWriter<SendMessageStreamEvent> writer,
        CancellationToken cancellationToken)
    {
        StartedMessageProcessing? processing = null;

        try
        {
            var validatedCommand = await validator.ValidateAsync(
                new SendMessageCommand(request.ConversationId, request.Message, request.Model),
                cancellationToken);
            var userContext = await userContextService.GetCurrentAsync(cancellationToken);
            var selectedModel = await modelSelector.SelectAsync(
                userContext.Organization.Id,
                validatedCommand.Model,
                cancellationToken);
            processing = await lifecycleService.StartAsync(
                validatedCommand.ConversationId,
                validatedCommand.Message,
                userContext.Organization,
                userContext.Member,
                cancellationToken);
            processing.SelectedModel = selectedModel;
            await writer.WriteAsync(
                new SendMessageStreamEvent(
                    SendMessageStreamEvent.Accepted,
                    CreateAcceptedPayload(processing)),
                cancellationToken);

            var availableTools = await toolRegistry.GetAvailableToolsAsync(
                userContext.Organization.Id,
                cancellationToken);
            var orchestrationResult = await orchestrator.OrchestrateStreamingAsync(
                processing,
                userContext.CreateConnectorExecutionContext(),
                selectedModel,
                processing.ConversationHistory,
                availableTools,
                async (message, token) => await writer.WriteAsync(
                    new SendMessageStreamEvent(
                        SendMessageStreamEvent.ProgressUpdated,
                        new { Message = message }),
                    token),
                async (delta, token) => await writer.WriteAsync(
                    new SendMessageStreamEvent(
                        SendMessageStreamEvent.AnswerDelta,
                        new { Delta = delta }),
                    token),
                cancellationToken);
            var completedProcessing = await lifecycleService.CompleteAsync(
                processing,
                orchestrationResult,
                cancellationToken);
            var response = responseFactory.Create(
                processing,
                orchestrationResult,
                completedProcessing);
            await writer.WriteAsync(
                new SendMessageStreamEvent(SendMessageStreamEvent.AnswerCompleted, response),
                cancellationToken);
            writer.TryComplete();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await FailProcessingAsync(processing, wasCancelled: true);
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            var errorCode = GetErrorCode(exception);
            errorReporter.Report(
                exception,
                processing?.ConversationId ?? request.ConversationId,
                processing?.UserMessageId,
                errorCode);
            await FailProcessingAsync(processing, wasCancelled: false);
            writer.TryWrite(new SendMessageStreamEvent(
                SendMessageStreamEvent.Error,
                new { Code = errorCode }));
            writer.TryComplete();
        }
    }

    /// <summary>
    /// Construit la charge utile du premier evenement. Le resume de la conversation
    /// n'y figure que lorsque l'envoi vient de la creer : le champ reste absent, et
    /// non nul, lorsque le client connait deja la conversation.
    /// </summary>
    private static object CreateAcceptedPayload(StartedMessageProcessing processing) =>
        processing.CreatedConversation is null
            ? new
            {
                processing.ConversationId,
                UserMessageId = processing.UserMessageId
            }
            : new
            {
                processing.ConversationId,
                UserMessageId = processing.UserMessageId,
                Conversation = processing.CreatedConversation
            };

    private static string GetErrorCode(Exception exception) => exception switch
    {
        AiProviderTimeoutException => "ai_provider_timeout",
        AiProviderLimitException => "ai_provider_limit",
        AiProviderUnavailableException => "ai_provider_unavailable",
        AiProviderInvalidResponseException => "ai_provider_invalid_response",
        _ => "message_generation_failed"
    };

    private async Task FailProcessingAsync(
        StartedMessageProcessing? processing,
        bool wasCancelled)
    {
        if (processing is null)
        {
            return;
        }

        await lifecycleService.FailAsync(
            processing,
            new MessageProcessingFailure(
                wasCancelled ? "message_generation_cancelled" : "message_generation_failed",
                wasCancelled),
            CancellationToken.None);
    }
}
