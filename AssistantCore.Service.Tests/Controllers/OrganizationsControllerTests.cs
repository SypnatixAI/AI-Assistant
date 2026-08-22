using System.Reflection;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.CreateOrganization;
using AssistantCore.Service.Application.Commands.CreateOrganization.Models;
using AssistantCore.Service.Application.Models.Organizations;
using AssistantCore.Service.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistantCore.Service.Tests.Controllers;

public sealed class OrganizationsControllerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AValidRequest_When_CreateOrganization_Then_DispatchesCommandAndReturnsCreated(
        CancellationToken cancellationToken,
        Guid organizationId,
        CreateOrganizationRequest request)
    {
        // Given
        var response = new OrganizationResponse(
            organizationId,
            request.Name,
            IdentityProvider.MicrosoftEntraId.ToString(),
            request.ExternalTenantId,
            RecordStatus.Active.ToString());
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new OrganizationsController(dispatcher);

        // When
        var actionResult = await controller.CreateOrganization(request, cancellationToken);

        // Then
        var createdResult = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        Assert.Same(response, createdResult.Value);
        var command = Assert.IsType<CreateOrganizationCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(request.Name, command.Name);
        Assert.Equal(request.IdentityProvider, command.IdentityProvider);
        Assert.Equal(request.ExternalTenantId, command.ExternalTenantId);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public void Given_TheCreateOrganizationAction_When_CreateOrganization_Then_RequiresAuthorizationAndUsesExpectedRoute(
        int _)
    {
        // Given
        var controllerType = typeof(OrganizationsController);

        // When
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var method = controllerType.GetMethod(nameof(OrganizationsController.CreateOrganization));

        // Then
        Assert.NotNull(method);
        Assert.Equal("api/organizations", controllerRoute?.Template);
        Assert.NotNull(authorizeAttribute);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
    }
}
