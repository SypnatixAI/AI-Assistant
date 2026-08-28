using System.Reflection;
using AssistantCore.Service.Application.Commands.GetConversationMessages;
using AssistantCore.Service.Application.Commands.ListConversations;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistantCore.Service.Tests.Controllers;

public sealed class ConversationsControllerTests
{
    [Fact]
    public async Task Given_AResponse_When_ListConversations_Then_DispatchesCommandAndReturnsOk()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var response = new ListConversationsResponse([], null, false);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new ConversationsController(dispatcher);

        // When
        var actionResult = await controller.ListConversations(50, "cursor-value", cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var command = Assert.IsType<ListConversationsCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(50, command.Limit);
        Assert.Equal("cursor-value", command.Cursor);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public void Given_TheListConversationsAction_When_ListConversations_Then_RequiresAuthorizationAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(ConversationsController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(ConversationsController.ListConversations));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/conversations", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
    }

    [Fact]
    public async Task Given_AResponse_When_GetMessages_Then_DispatchesCommandAndReturnsOk()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var conversationId = Guid.NewGuid();
        var response = new GetConversationMessagesResponse(conversationId, [], null, false);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new ConversationsController(dispatcher);

        // When
        var actionResult = await controller.GetMessages(conversationId, 50, "cursor-value", cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var command = Assert.IsType<GetConversationMessagesCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(conversationId, command.ConversationId);
        Assert.Equal(50, command.Limit);
        Assert.Equal("cursor-value", command.Cursor);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public void Given_TheGetMessagesAction_When_GetMessages_Then_RequiresAuthorizationAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(ConversationsController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(ConversationsController.GetMessages));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/conversations", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.Equal("{conversationId}/messages", method.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }
}
