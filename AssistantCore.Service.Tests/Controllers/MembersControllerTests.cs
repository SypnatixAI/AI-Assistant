using System.Reflection;
using AssistantCore.Service.Application.Commands.GetMembers;
using AssistantCore.Service.Application.Commands.GetMembers.Models;
using AssistantCore.Service.Application.Commands.UpdateMemberRole;
using AssistantCore.Service.Application.Commands.UpdateMemberRole.Models;
using AssistantCore.Service.Application.Models.Members;
using AssistantCore.Service.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistantCore.Service.Tests.Controllers;

public sealed class MembersControllerTests
{
    [Fact]
    public async Task Given_AResponse_When_GetMembers_Then_DispatchesCommandAndReturnsOk()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var response = new GetMembersResponse(
        [
            new MemberResponse(
                Guid.NewGuid(),
                "Admin User",
                "admin@example.com",
                "Admin",
                "Active")
        ]);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new MembersController(dispatcher);

        // When
        var actionResult = await controller.GetMembers(cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.IsType<GetMembersCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public void Given_TheGetMembersAction_When_GetMembers_Then_RequiresAuthorizationAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(MembersController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(MembersController.GetMembers));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/members", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
    }

    [Fact]
    public async Task Given_AValidRequest_When_UpdateMemberRole_Then_DispatchesCommandAndReturnsOk()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var memberId = Guid.NewGuid();
        var response = new MemberResponse(
            memberId,
            "Updated Member",
            "updated@example.com",
            "Admin",
            "Active");
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new MembersController(dispatcher);

        // When
        var actionResult = await controller.UpdateMemberRole(
            memberId,
            new UpdateMemberRoleRequest("Admin"),
            cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var command = Assert.IsType<UpdateMemberRoleCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(memberId, command.MemberId);
        Assert.Equal("Admin", command.Role);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public void Given_TheUpdateMemberRoleAction_When_UpdateMemberRole_Then_RequiresAuthorizationAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(MembersController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(MembersController.UpdateMemberRole));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/members", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.Equal("{memberId}/role", method.GetCustomAttribute<HttpPatchAttribute>()?.Template);
    }
}
