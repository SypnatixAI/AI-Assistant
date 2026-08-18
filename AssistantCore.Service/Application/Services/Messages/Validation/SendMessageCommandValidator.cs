using AssistantCore.Service.Application.Commands.SendMessage;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Messages.Validation;

public sealed class SendMessageCommandValidator :
    AbstractValidator<SendMessageCommand>,
    ISendMessageCommandValidator
{
    public SendMessageCommandValidator(
        IOptions<MessagesOptions> options,
        IAuthorizedAiModelSelector aiModelSelector)
    {
        RuleFor(command => command.Message)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(message => message.Trim().Length <= options.Value.MaximumMessageLength)
            .WithMessage(
                $"Message must not exceed {options.Value.MaximumMessageLength} characters.");

        RuleFor(command => command.Model)
            .Must(aiModelSelector.IsAvailable)
            .WithMessage("The requested AI model is not available.");
    }

    async Task<SendMessageCommand> ISendMessageCommandValidator.ValidateAsync(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new BadRequestException(validationResult.Errors[0].ErrorMessage);
        }

        return command with
        {
            Message = command.Message.Trim(),
            Model = string.IsNullOrWhiteSpace(command.Model)
                ? null
                : command.Model.Trim()
        };
    }
}
