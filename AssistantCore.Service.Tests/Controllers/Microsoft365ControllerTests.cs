using AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent;
using AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent.Models;
using AssistantCore.Service.Application.Commands.StartMicrosoft365Consent;
using AssistantCore.Service.Application.Commands.StartMicrosoft365Consent.Models;
using AssistantCore.Service.Application.Commands.RevokeMicrosoft365Connection;
using AssistantCore.Service.Application.Commands.RevokeMicrosoft365Connection.Models;
using AssistantCore.Service.Application.Commands.GetMicrosoft365SiteLists;
using AssistantCore.Service.Application.Commands.GetMicrosoft365SiteLists.Models;
using AssistantCore.Service.Application.Commands.EnableMicrosoft365List;
using AssistantCore.Service.Application.Commands.EnableMicrosoft365List.Models;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace AssistantCore.Service.Tests.Controllers;

public sealed class Microsoft365ControllerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnAuthorizationUrl_When_StartConsent_Then_DispatchesCommandAndReturnsOk(
        string authorizationUrl,
        CancellationToken cancellationToken)
    {
        // Given
        var response = new StartMicrosoft365ConsentResponse(authorizationUrl);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new Microsoft365Controller(dispatcher);

        // When
        var actionResult = await controller.StartConsent(cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        Assert.IsType<StartMicrosoft365ConsentCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_AConsentCallback_When_CompleteConsent_Then_DispatchesCallbackValues(
        Guid connectionId,
        Guid tenantId,
        string state,
        CancellationToken cancellationToken)
    {
        // Given
        var response = new CompleteMicrosoft365ConsentResponse(
            connectionId,
            tenantId.ToString("D"),
            "Active");
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new Microsoft365Controller(dispatcher);

        // When
        var actionResult = await controller.CompleteConsent(
            tenantId.ToString("D"),
            true,
            state,
            null,
            cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        var command = Assert.IsType<CompleteMicrosoft365ConsentCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(tenantId.ToString("D"), command.TenantId);
        Assert.True(command.AdminConsent);
        Assert.Equal(state, command.State);
        Assert.Null(command.MicrosoftError);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_AConnectionId_When_RevokeConnection_Then_DispatchesCommandAndReturnsOk(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        // Given
        var response = new RevokeMicrosoft365ConnectionResponse(connectionId, "Revoked");
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new Microsoft365Controller(dispatcher);

        // When
        var actionResult = await controller.RevokeConnection(connectionId, cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        var command = Assert.IsType<RevokeMicrosoft365ConnectionCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(connectionId, command.ConnectionId);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_ASiteId_When_GetSiteLists_Then_DispatchesCommandAndReturnsOk(
        string siteId,
        CancellationToken cancellationToken)
    {
        // Given
        var response = new GetMicrosoft365SiteListsResponse([]);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new Microsoft365Controller(dispatcher);

        // When
        var actionResult = await controller.GetSiteLists(siteId, cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        var command = Assert.IsType<GetMicrosoft365SiteListsCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(siteId, command.SiteId);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_AListActivation_When_EnableList_Then_DispatchesCommandAndReturnsOk(
        string siteId,
        string listId,
        CancellationToken cancellationToken)
    {
        // Given
        var response = new Microsoft365ListResponse(
            siteId,
            listId,
            "Requests",
            null,
            "Enabled",
            true);
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new Microsoft365Controller(dispatcher);

        // When
        var actionResult = await controller.EnableList(
            siteId,
            listId,
            new EnableMicrosoft365ListRequest(true),
            cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        var command = Assert.IsType<EnableMicrosoft365ListCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(siteId, command.SiteId);
        Assert.Equal(listId, command.ListId);
        Assert.True(command.IsIndexed);
        Assert.Equal(cancellationToken, dispatcher.ReceivedCancellationToken);
    }
}
