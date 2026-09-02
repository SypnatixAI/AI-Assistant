using System.Reflection;
using AssistantCore.Service.Application.Commands.DeleteConversation;
using AssistantCore.Service.Application.Commands.DeleteConversation.Models;
using AssistantCore.Service.Application.Commands.GetConversationMessages;
using AssistantCore.Service.Application.Commands.ListConversations;
using AssistantCore.Service.Application.Commands.UpdateConversation;
using AssistantCore.Service.Application.Commands.UpdateConversation.Models;
using AssistantCore.Service.Application.Exceptions;
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
        var actionResult = await controller.ListConversations(50, "cursor-value", "Archived", cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var command = Assert.IsType<ListConversationsCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(50, command.Limit);
        Assert.Equal("cursor-value", command.Cursor);
        Assert.Equal("Archived", command.Status);
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

    [Fact]
    public async Task Given_AnIfMatchHeader_When_UpdateConversation_Then_DispatchesTheExpectedVersionAndReturnsOk()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var conversationId = Guid.NewGuid();
        var response = new ConversationResponse(
            conversationId,
            "Politique de teletravail",
            "Archived",
            DateTimeOffset.UtcNow,
            8);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = CreateController(dispatcher, ifMatch: "\"7\"");

        // When
        var actionResult = await controller.UpdateConversation(
            conversationId,
            new UpdateConversationRequest("Politique de teletravail", "Archived"),
            cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        var command = Assert.IsType<UpdateConversationCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(conversationId, command.ConversationId);
        Assert.Equal("Politique de teletravail", command.Title);
        Assert.Equal("Archived", command.Status);
        Assert.Equal(7, command.ExpectedVersion);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Given_NoIfMatchHeader_When_UpdateConversation_Then_DispatchesWithoutAnExpectedVersion()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var response = new ConversationResponse(
            conversationId,
            "Titre",
            "Active",
            DateTimeOffset.UtcNow,
            1);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = CreateController(dispatcher, ifMatch: null);

        // When
        await controller.UpdateConversation(
            conversationId,
            new UpdateConversationRequest("Titre", null),
            CancellationToken.None);

        // Then
        var command = Assert.IsType<UpdateConversationCommand>(dispatcher.ReceivedRequest);
        Assert.Null(command.ExpectedVersion);
    }

    [Fact]
    public async Task Given_AnUnreadableIfMatchHeader_When_UpdateConversation_Then_ThrowsBadRequestException()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var response = new ConversationResponse(
            conversationId,
            "Titre",
            "Active",
            DateTimeOffset.UtcNow,
            1);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = CreateController(dispatcher, ifMatch: "\"not-a-version\"");

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            controller.UpdateConversation(
                conversationId,
                new UpdateConversationRequest("Titre", null),
                CancellationToken.None));
    }

    [Fact]
    public void Given_TheUpdateConversationAction_When_UpdateConversation_Then_RequiresAuthorizationAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(ConversationsController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(ConversationsController.UpdateConversation));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/conversations", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.Equal("{conversationId}", method.GetCustomAttribute<HttpPatchAttribute>()?.Template);
    }

    [Fact]
    public async Task Given_AConversation_When_DeleteConversation_Then_DispatchesCommandAndReturnsNoContent()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var conversationId = Guid.NewGuid();
        var dispatcher = new RecordingDispatcher
        {
            Response = new DeleteConversationResponse(conversationId, AlreadyDeleted: false)
        };
        var controller = CreateController(dispatcher, ifMatch: null);

        // When
        var actionResult = await controller.DeleteConversation(conversationId, cancellationToken);

        // Then
        Assert.IsType<NoContentResult>(actionResult);
        var command = Assert.IsType<DeleteConversationCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(conversationId, command.ConversationId);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public void Given_TheDeleteConversationAction_When_DeleteConversation_Then_RequiresAuthorizationAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(ConversationsController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(ConversationsController.DeleteConversation));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/conversations", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.Equal("{conversationId}", method.GetCustomAttribute<HttpDeleteAttribute>()?.Template);
    }

    private static ConversationsController CreateController(
        RecordingDispatcher dispatcher,
        string? ifMatch)
    {
        var httpContext = new DefaultHttpContext();

        if (ifMatch is not null)
        {
            httpContext.Request.Headers.IfMatch = ifMatch;
        }

        return new ConversationsController(dispatcher)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }
}
