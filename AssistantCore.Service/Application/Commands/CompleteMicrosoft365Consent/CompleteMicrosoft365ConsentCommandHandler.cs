using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent.Models;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent;

public sealed class CompleteMicrosoft365ConsentCommandHandler(
    IMicrosoft365ConnectionService connectionService)
    : IRequestHandler<CompleteMicrosoft365ConsentCommand, CompleteMicrosoft365ConsentResponse>
{
    public async Task<CompleteMicrosoft365ConsentResponse> HandleAsync(
        CompleteMicrosoft365ConsentCommand request,
        CancellationToken cancellationToken)
    {
        var result = await connectionService.CompleteConsentAsync(
            request.TenantId,
            request.AdminConsent,
            request.State,
            request.MicrosoftError,
            cancellationToken);

        return new CompleteMicrosoft365ConsentResponse(
            result.ConnectionId,
            result.TenantId,
            result.Status.ToString());
    }
}
