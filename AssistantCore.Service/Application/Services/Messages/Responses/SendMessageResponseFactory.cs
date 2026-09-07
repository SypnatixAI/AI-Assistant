using AssistantCore.Service.Application.Commands.SendMessage.Models;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Services.Messages.Responses;

public sealed class SendMessageResponseFactory : ISendMessageResponseFactory
{
    public SendMessageResponse Create(
        StartedMessageProcessing processing,
        MessageOrchestrationResult orchestrationResult,
        CompletedMessageProcessing completedProcessing) =>
        new(
            processing.ConversationId,
            completedProcessing.AssistantMessageId,
            orchestrationResult.Answer,
            orchestrationResult.ModelName,
            orchestrationResult.CitedEvidence.Select(MapSource).ToArray(),
            orchestrationResult.Warnings.Where(IsUserFacingWarning).ToArray(),
            completedProcessing.CreatedAt,
            completedProcessing.Usage);

    private static MessageSourceResponse MapSource(RetrievedEvidence evidence) =>
        new(
            evidence.SourceType,
            evidence.Title,
            evidence.Url,
            evidence.Reference);

    private static bool IsUserFacingWarning(string warning) =>
        !string.IsNullOrWhiteSpace(warning)
        && !warning.StartsWith("rag.", StringComparison.OrdinalIgnoreCase);
}
