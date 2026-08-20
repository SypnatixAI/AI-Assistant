using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.StartMicrosoft365Consent.Models;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.StartMicrosoft365Consent;

public sealed class StartMicrosoft365ConsentCommandHandler(
    IMicrosoft365ConnectionService connectionService)
    : IRequestHandler<StartMicrosoft365ConsentCommand, StartMicrosoft365ConsentResponse>
{
    public async Task<StartMicrosoft365ConsentResponse> HandleAsync(
        StartMicrosoft365ConsentCommand request,
        CancellationToken cancellationToken)
    {
        var authorizationUri = await connectionService.StartConsentAsync(cancellationToken);
        return new StartMicrosoft365ConsentResponse(authorizationUri.AbsoluteUri);
    }
}
