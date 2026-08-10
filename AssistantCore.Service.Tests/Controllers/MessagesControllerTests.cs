using System.Reflection;
using AssistantCore.Service.Application.Commands.SendMessage;
using AssistantCore.Service.Application.Commands.SendMessage.Models;
using AssistantCore.Service.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistantCore.Service.Tests.Controllers;

public sealed class MessagesControllerTests
{
    [Fact]
    public async Task Given_AValidRequest_When_SendMessage_Then_DispatchesCommandAndReturnsOk()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var conversationId = Guid.NewGuid();
        var response = new SendMessageResponse(
            conversationId,
            Guid.NewGuid(),
            "Response",
            "gpt",
            [],
            [],
            DateTimeOffset.UtcNow);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new MessagesController(dispatcher);
        var request = new SendMessageRequest(conversationId, "Question", "gpt");

        // When
        var actionResult = await controller.SendMessage(request, cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var command = Assert.IsType<SendMessageCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(conversationId, command.ConversationId);
        Assert.Equal("Question", command.Message);
        Assert.Equal("gpt", command.Model);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public void Given_TheSendMessageAction_When_SendMessage_Then_RequiresAuthorizationAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(MessagesController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(MessagesController.SendMessage));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/messages", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
    }
}
