using AssistantCore.Service.Application.Commands.SendMessage.Models;

namespace AssistantCore.Service.Application.Services.Messages;

public interface ISendMessageService
{
    Task<SendMessageResponse> SendMessageAsync(
        Guid? conversationId,
        string message,
        string? model,
        CancellationToken cancellationToken);
}
