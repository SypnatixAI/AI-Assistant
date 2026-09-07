using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.SendMessage.Models;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.Authorization;
using AssistantCore.Service.Application.Services.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Responses;
using AssistantCore.Service.Application.Services.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Validation;
using System.Diagnostics;

namespace AssistantCore.Service.Application.Commands.SendMessage;

public sealed class SendMessageCommandHandler(
    ISendMessageCommandValidator validator,
    IMessageUserContextService userContextService,
    IAuthorizedAiModelSelector modelSelector,
    IMessageProcessingLifecycleService lifecycleService,
    IAiToolRegistry toolRegistry,
    IMessageToolOrchestrator orchestrator,
    ISendMessageResponseFactory responseFactory)
    : IRequestHandler<SendMessageCommand, SendMessageResponse>
{
    public async Task<SendMessageResponse> HandleAsync(
        SendMessageCommand request,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        using var activity = MessageTelemetry.Activities.StartActivity("messages.process");
        activity?.SetTag("messages.operation", "send");
        StartedMessageProcessing? processing = null;

        try
        {
            var validatedCommand = await validator.ValidateAsync(request, cancellationToken);
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
            var availableTools = await toolRegistry.GetAvailableToolsAsync(
                userContext.Organization.Id,
                cancellationToken);
            var orchestrationResult = await orchestrator.OrchestrateAsync(
                processing,
                userContext.CreateConnectorExecutionContext(),
                selectedModel,
                processing.ConversationHistory,
                availableTools,
                cancellationToken);
            var completedProcessing = await lifecycleService.CompleteAsync(
                processing,
                orchestrationResult,
                cancellationToken);

            activity?.SetTag("messages.outcome", "completed");
            return responseFactory.Create(
                processing,
                orchestrationResult,
                completedProcessing);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetTag("messages.outcome", "cancelled");
            await FailProcessingAsync(processing, wasCancelled: true);
            throw;
        }
        catch
        {
            activity?.SetTag("messages.outcome", "failed");
            await FailProcessingAsync(processing, wasCancelled: false);
            throw;
        }
        finally
        {
            MessageTelemetry.RecordDuration(
                "messages.process.duration_ms",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

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
