using System.Reflection;
using AssistantCore.Service.Application.Commands.SetUsagePolicy;
using AssistantCore.Service.Application.Commands.SetUsagePolicy.Models;
using AssistantCore.Service.Application.Models.Usage;
using AssistantCore.Service.Controllers;
using AssistantCore.Service.Infrastructure.Authentication.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistantCore.Service.Tests.Controllers;

public sealed class InternalOrganizationsControllerTests
{
    [Fact]
    public async Task Given_AValidRequest_When_SetUsagePolicy_Then_DispatchesCommandAndReturnsOk()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var organizationId = Guid.NewGuid();
        var response = new UsagePolicyResponse(
            organizationId,
            3,
            1_000_000,
            "Active",
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-08-18T15:00:00Z"));
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new InternalOrganizationsController(dispatcher);

        // When
        var actionResult = await controller.SetUsagePolicy(
            organizationId,
            new SetUsagePolicyRequest(1_000_000, "Active", "2026-09-01T00:00:00Z"),
            cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var command = Assert.IsType<SetUsagePolicyCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(organizationId, command.OrganizationId);
        Assert.Equal(1_000_000, command.MonthlyTokenLimit);
        Assert.Equal("Active", command.Status);
        Assert.Equal("2026-09-01T00:00:00Z", command.EffectiveAt);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Fact]
    public void Given_TheSetUsagePolicyAction_When_SetUsagePolicy_Then_RequiresTheDedicatedPolicyAndUsesExpectedRoute()
    {
        // Given
        var controllerType = typeof(InternalOrganizationsController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(InternalOrganizationsController.SetUsagePolicy));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/internal/organizations", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.Equal(UsagePolicyAuthorizationPolicies.Management, authorizeAttribute.Policy);
        Assert.Equal(
            "{organizationId}/usage-policy",
            method.GetCustomAttribute<HttpPutAttribute>()?.Template);
    }
}
