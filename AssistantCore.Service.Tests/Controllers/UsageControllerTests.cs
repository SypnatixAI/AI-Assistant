using System.Reflection;
using AssistantCore.Service.Application.Commands.GetTokenUsage;
using AssistantCore.Service.Application.Models.Usage;
using AssistantCore.Service.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistantCore.Service.Tests.Controllers;

public sealed class UsageControllerTests
{
    [Fact]
    public async Task Given_AResponse_When_GetTokenUsage_Then_DispatchesCommandAndReturnsOk()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var response = new TokenUsageResponse(
            DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            1_000_000,
            428_000,
            572_000,
            false);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new UsageController(dispatcher);

        // When
        var actionResult = await controller.GetTokenUsage(cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.IsType<GetTokenUsageCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public void Given_TheGetTokenUsageAction_When_GetTokenUsage_Then_RequiresAuthorizationAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(UsageController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(UsageController.GetTokenUsage));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/usage", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
    }
}
