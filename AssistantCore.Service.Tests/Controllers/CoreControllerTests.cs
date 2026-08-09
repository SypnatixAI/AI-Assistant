using System.Reflection;
using AssistantCore.Service.Application.Commands.AuthenticateUser;
using AssistantCore.Service.Application.Commands.AuthenticateUser.Models;
using AssistantCore.Service.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistantCore.Service.Tests.Controllers;

public sealed class CoreControllerTests
{
    [Fact]
    public async Task Given_AValidAuthenticationResponse_When_AuthenticatingUser_Then_DispatchesCommandAndReturnsOk()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var response = new AuthenticateUserResponse(
            new CurrentUserResponse(Guid.NewGuid(), "Test User", "test@example.com"),
            new CurrentOrganizationResponse(Guid.NewGuid(), "Contoso"),
            ["User"]);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new CoreController(dispatcher);

        // When
        var actionResult = await controller.AuthenticateUser(cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.IsType<AuthenticateUserCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public void Given_TheAuthenticateUserAction_When_InspectingMetadata_Then_RequiresAuthorizationAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(CoreController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var method = controllerType.GetMethod(nameof(CoreController.AuthenticateUser));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/[controller]", controllerRoute?.Template);
        Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("authenticateUser", method.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }
}
