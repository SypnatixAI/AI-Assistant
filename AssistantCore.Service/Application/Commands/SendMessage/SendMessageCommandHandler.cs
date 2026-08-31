using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.SendMessage.Models;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.Authorization;
using AssistantCore.Service.Application.Services.Messages.Lifecycle;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Responses;
using AssistantCore.Service.Application.Services.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Validation;

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
        var validatedCommand = await validator.ValidateAsync(request, cancellationToken);
        var userContext = await userContextService.GetCurrentAsync(cancellationToken);
        var selectedModel = await modelSelector.SelectAsync(
            userContext.Organization.Id,
            validatedCommand.Model,
            cancellationToken);
        var processing = await lifecycleService.StartAsync(
            validatedCommand.ConversationId,
            validatedCommand.Message,
            userContext.Organization,
            userContext.Member,
            cancellationToken);
        processing.SelectedModel = selectedModel;
        var availableTools = await toolRegistry.GetAvailableToolsAsync(
            userContext.Organization.Id,
            cancellationToken);
        var executionContext = new ConnectorExecutionContext(
            userContext.Organization.Id,
            userContext.Member.Id,
            userContext.Organization.ExternalTenantId,
            Guid.TryParse(userContext.Member.ExternalUserId, out var entraUserId)
                ? entraUserId
                : null,
            userContext.Member.IdentityProvider);
        var orchestrationResult = await orchestrator.OrchestrateAsync(
            processing,
            executionContext,
            selectedModel,
            processing.ConversationHistory,
            availableTools,
            cancellationToken);
        var completedProcessing = await lifecycleService.CompleteAsync(
            processing,
            orchestrationResult,
            cancellationToken);

        return responseFactory.Create(
            processing,
            orchestrationResult,
            completedProcessing);
    }
}
