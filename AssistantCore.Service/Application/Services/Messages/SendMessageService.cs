using AssistantCore.Service.Application.Commands.SendMessage.Models;

namespace AssistantCore.Service.Application.Services.Messages;

public sealed class SendMessageService : ISendMessageService
{
    public Task<SendMessageResponse> SendMessageAsync(
        Guid? conversationId,
        string message,
        string? model,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "The message flow has not been implemented yet.");
    }
}
