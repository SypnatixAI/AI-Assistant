using AssistantCore.Service.Application.Commands.SendMessage;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.AiModels;
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
            "  Valid question  ",
            "  gpt-available  ");

        // When
        var result = await validator.ValidateAsync(command, CancellationToken.None);

        // Then
        Assert.Equal(conversationId, result.ConversationId);
        Assert.Equal("Valid question", result.Message);
        Assert.Equal("gpt-available", result.Model);
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
            var command = new SendMessageCommand(conversationId, message!, null);

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
        var command = new SendMessageCommand(conversationId, message, null);

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            validator.ValidateAsync(command, CancellationToken.None));

        // Then
        Assert.Equal(
            $"Message must not exceed {MaximumMessageLength} characters.",
            exception.Message);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnavailableModel_When_ValidateAsync_Then_ThrowsBadRequest(
        Guid conversationId)
    {
        // Given
        ISendMessageCommandValidator validator = CreateValidator();
        var command = new SendMessageCommand(
            conversationId,
            "Valid question",
            "gpt-unavailable");

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            validator.ValidateAsync(command, CancellationToken.None));

        // Then
        Assert.Equal("The requested AI model is not available.", exception.Message);
    }

    private static SendMessageCommandValidator CreateValidator()
    {
        var options = Options.Create(new MessagesOptions
        {
            MaximumMessageLength = MaximumMessageLength
        });

        return new SendMessageCommandValidator(
            options,
            new StubAuthorizedAiModelSelector());
    }

    private sealed class StubAuthorizedAiModelSelector : IAuthorizedAiModelSelector
    {
        public bool IsAvailable(string? requestedModel) =>
            string.IsNullOrWhiteSpace(requestedModel)
            || string.Equals(
                requestedModel.Trim(),
                "gpt-available",
                StringComparison.OrdinalIgnoreCase);

        public Task<SelectedAiModel> SelectAsync(
            Guid organizationId,
            string? requestedModel,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
