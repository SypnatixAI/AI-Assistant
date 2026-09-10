using AssistantCore.Service.Application.Commands.SendMessage;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Messages.Validation;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Messages;

public sealed class SendMessageCommandValidatorTests
{
    private const int MaximumMessageLength = 20;

    [Theory, AutoDomainData]
    public async Task Given_AValidCommand_When_ValidateAsync_Then_ReturnsNormalizedCommand(
        Guid conversationId)
    {
        // Given
        ISendMessageCommandValidator validator = CreateValidator();
        var command = new SendMessageCommand(
            conversationId,
            "  Valid question  ");

        // When
        var result = await validator.ValidateAsync(command, CancellationToken.None);

        // Then
        Assert.Equal(conversationId, result.ConversationId);
        Assert.Equal("Valid question", result.Message);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyMessage_When_ValidateAsync_Then_ThrowsBadRequest(
        Guid conversationId)
    {
        // Given
        ISendMessageCommandValidator validator = CreateValidator();
        string?[] invalidMessages = [null, string.Empty, "   "];

        foreach (var message in invalidMessages)
        {
            var command = new SendMessageCommand(conversationId, message!);

            // When
            var exception = await Record.ExceptionAsync(() =>
                validator.ValidateAsync(command, CancellationToken.None));

            // Then
            Assert.IsType<BadRequestException>(exception);
        }
    }

    [Theory, AutoDomainData]
    public async Task Given_AMessageLongerThanConfiguredMaximum_When_ValidateAsync_Then_ThrowsBadRequest(
        Guid conversationId)
    {
        // Given
        ISendMessageCommandValidator validator = CreateValidator();
        var message = $"  {new string('a', MaximumMessageLength + 1)}  ";
        var command = new SendMessageCommand(conversationId, message);

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            validator.ValidateAsync(command, CancellationToken.None));

        // Then
        Assert.Equal(
            $"Message must not exceed {MaximumMessageLength} characters.",
            exception.Message);
    }

    private static SendMessageCommandValidator CreateValidator()
    {
        var options = Options.Create(new MessagesOptions
        {
            MaximumMessageLength = MaximumMessageLength
        });

        return new SendMessageCommandValidator(options);
    }
}
