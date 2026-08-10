using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.SendMessage.Models;
using AssistantCore.Service.Application.Services.Messages;

namespace AssistantCore.Service.Application.Commands.SendMessage;

public sealed class SendMessageCommandHandler(
    ISendMessageService sendMessageService)
    : IRequestHandler<SendMessageCommand, SendMessageResponse>
{
    public Task<SendMessageResponse> HandleAsync(
        SendMessageCommand request,
        CancellationToken cancellationToken)
    {
        return sendMessageService.SendMessageAsync(
            request.ConversationId,
            request.Message,
            request.Model,
            cancellationToken);
    }
}
