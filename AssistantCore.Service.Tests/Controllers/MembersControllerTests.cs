using System.Reflection;
using AssistantCore.Service.Application.Commands.GetMembers;
using AssistantCore.Service.Application.Commands.GetMembers.Models;
using AssistantCore.Service.Application.Commands.UpdateMemberRole;
using AssistantCore.Service.Application.Commands.UpdateMemberRole.Models;
using AssistantCore.Service.Application.Commands.UpdateMemberStatus;
using AssistantCore.Service.Application.Commands.UpdateMemberStatus.Models;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Members;
using AssistantCore.Service.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
                "Active",
                1)
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
            "Active",
            1);
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

    [Fact]
    public async Task Given_AnIfMatchHeader_When_UpdateMemberStatus_Then_DispatchesTheExpectedVersionAndReturnsOk()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var memberId = Guid.NewGuid();
        var response = new MemberResponse(
            memberId,
            "Bob Martin",
            "bob@contoso.com",
            "User",
            "Inactive",
            4);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = CreateController(dispatcher, ifMatch: "\"3\"");

        // When
        var actionResult = await controller.UpdateMemberStatus(
            memberId,
            new UpdateMemberStatusRequest("Inactive"),
            cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var command = Assert.IsType<UpdateMemberStatusCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(memberId, command.MemberId);
        Assert.Equal("Inactive", command.Status);
        Assert.Equal(3, command.ExpectedVersion);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Given_NoIfMatchHeader_When_UpdateMemberStatus_Then_DispatchesWithoutAnExpectedVersion()
    {
        // Given
        var memberId = Guid.NewGuid();
        var response = new MemberResponse(
            memberId,
            "Bob Martin",
            "bob@contoso.com",
            "User",
            "Active",
            1);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = CreateController(dispatcher, ifMatch: null);

        // When
        await controller.UpdateMemberStatus(
            memberId,
            new UpdateMemberStatusRequest("Active"),
            CancellationToken.None);

        // Then
        var command = Assert.IsType<UpdateMemberStatusCommand>(dispatcher.ReceivedRequest);
        Assert.Null(command.ExpectedVersion);
    }

    [Fact]
    public async Task Given_AnUnreadableIfMatchHeader_When_UpdateMemberStatus_Then_ThrowsBadRequestException()
    {
        // Given
        var memberId = Guid.NewGuid();
        var response = new MemberResponse(
            memberId,
            "Bob Martin",
            "bob@contoso.com",
            "User",
            "Active",
            1);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = CreateController(dispatcher, ifMatch: "\"not-a-version\"");

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            controller.UpdateMemberStatus(
                memberId,
                new UpdateMemberStatusRequest("Inactive"),
                CancellationToken.None));
    }

    [Fact]
    public void Given_TheUpdateMemberStatusAction_When_UpdateMemberStatus_Then_RequiresAuthorizationAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(MembersController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(MembersController.UpdateMemberStatus));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/members", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.Equal("{memberId}/status", method.GetCustomAttribute<HttpPatchAttribute>()?.Template);
    }

    private static MembersController CreateController(
        RecordingDispatcher dispatcher,
        string? ifMatch)
    {
        var httpContext = new DefaultHttpContext();

        if (ifMatch is not null)
        {
            httpContext.Request.Headers.IfMatch = ifMatch;
        }

        return new MembersController(dispatcher)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }
}
