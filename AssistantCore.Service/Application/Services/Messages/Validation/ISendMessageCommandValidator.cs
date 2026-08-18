using AssistantCore.Service.Application.Commands.SendMessage;

namespace AssistantCore.Service.Application.Services.Messages.Validation;

public interface ISendMessageCommandValidator
{
    Task<SendMessageCommand> ValidateAsync(
        SendMessageCommand command,
        CancellationToken cancellationToken);
}
