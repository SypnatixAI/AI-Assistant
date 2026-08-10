using AssistantCore.Service.Application.Commands.SendMessage;
using AssistantCore.Service.Application.Commands.SendMessage.Models;
using AssistantCore.Service.Application.Services.Messages;

namespace AssistantCore.Service.Tests.Messages;

public sealed class SendMessageCommandHandlerTests
{
    [Fact]
    public async Task Given_AServiceResponse_When_HandleAsync_Then_DelegatesRequestAndReturnsResponse()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var conversationId = Guid.NewGuid();
        var response = CreateResponse(conversationId);
        var service = new StubSendMessageService { Response = response };
        var handler = new SendMessageCommandHandler(service);
        var command = new SendMessageCommand(conversationId, "Question", "gpt");

        // When
        var result = await handler.HandleAsync(command, cancellationToken);

        // Then
        Assert.Same(response, result);
        Assert.Equal(conversationId, service.ReceivedConversationId);
        Assert.Equal("Question", service.ReceivedMessage);
        Assert.Equal("gpt", service.ReceivedModel);
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
    }

    private static SendMessageResponse CreateResponse(Guid conversationId)
    {
        return new SendMessageResponse(
            conversationId,
            Guid.NewGuid(),
            "Response",
            "gpt",
            [],
            [],
            DateTimeOffset.UtcNow);
    }

    private sealed class StubSendMessageService : ISendMessageService
    {
        public required SendMessageResponse Response { get; init; }

        public Guid? ReceivedConversationId { get; private set; }

        public string? ReceivedMessage { get; private set; }

        public string? ReceivedModel { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<SendMessageResponse> SendMessageAsync(
            Guid? conversationId,
            string message,
            string? model,
            CancellationToken cancellationToken)
        {
            ReceivedConversationId = conversationId;
            ReceivedMessage = message;
            ReceivedModel = model;
            ReceivedCancellationToken = cancellationToken;

            return Task.FromResult(Response);
        }
    }
}
