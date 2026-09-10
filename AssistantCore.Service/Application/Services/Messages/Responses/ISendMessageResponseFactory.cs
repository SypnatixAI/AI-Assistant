using AssistantCore.Service.Application.Commands.SendMessage.Models;
using AssistantCore.Service.Application.Models.Messages.AgentRuntime;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;

namespace AssistantCore.Service.Application.Services.Messages.Responses;

public interface ISendMessageResponseFactory
{
    SendMessageResponse Create(
        StartedMessageProcessing processing,
        AgentTurnResult agentTurnResult,
        CompletedMessageProcessing completedProcessing);
}
