using System.Reflection;
using AssistantCore.Service.Application.Commands.GetAvailableModels;
using AssistantCore.Service.Application.Models.Models;
using AssistantCore.Service.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistantCore.Service.Tests.Controllers;

public sealed class ModelsControllerTests
{
    [Fact]
    public async Task Given_AResponse_When_GetAvailableModels_Then_DispatchesCommandAndReturnsOk()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var response = new GetAvailableModelsResponse(
            "gpt-5.6-luna",
            [new AvailableModelResponse("gpt-5.6-luna", "Luna", "Modele general.", true)]);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new ModelsController(dispatcher);

        // When
        var actionResult = await controller.GetAvailableModels(cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.IsType<GetAvailableModelsCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public void Given_TheGetAvailableModelsAction_When_GetAvailableModels_Then_RequiresAuthorizationAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(ModelsController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(ModelsController.GetAvailableModels));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/models", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
    }
}
