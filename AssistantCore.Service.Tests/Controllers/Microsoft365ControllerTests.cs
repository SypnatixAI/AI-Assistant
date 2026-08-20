using AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent;
using AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent.Models;
using AssistantCore.Service.Application.Commands.StartMicrosoft365Consent;
using AssistantCore.Service.Application.Commands.StartMicrosoft365Consent.Models;
using AssistantCore.Service.Application.Commands.RevokeMicrosoft365Connection;
using AssistantCore.Service.Application.Commands.RevokeMicrosoft365Connection.Models;
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
        string tenantId,
        string code,
        string state,
        CancellationToken cancellationToken)
    {
        // Given
        var response = new CompleteMicrosoft365ConsentResponse(connectionId, tenantId, "Active");
        var dispatcher = new RecordingDispatcher { Response = response };
        var controller = new Microsoft365Controller(dispatcher);

        // When
        var actionResult = await controller.CompleteConsent(
            code,
            state,
            null,
            cancellationToken);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(response, okResult.Value);
        var command = Assert.IsType<CompleteMicrosoft365ConsentCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(code, command.Code);
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
}
