using AssistantCore.Service.Application.Commands.SendMessage.Models;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Services.Messages.Responses;

public interface ISendMessageResponseFactory
{
    SendMessageResponse Create(
        StartedMessageProcessing processing,
        MessageOrchestrationResult orchestrationResult,
        CompletedMessageProcessing completedProcessing);
}
