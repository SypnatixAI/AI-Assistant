using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetMicrosoft365OnboardingStatus.Models;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.GetMicrosoft365OnboardingStatus;

public sealed class GetMicrosoft365OnboardingStatusCommandHandler(
    IMicrosoft365OnboardingService onboardingService)
    : IRequestHandler<
        GetMicrosoft365OnboardingStatusCommand,
        GetMicrosoft365OnboardingStatusResponse>
{
    public async Task<GetMicrosoft365OnboardingStatusResponse> HandleAsync(
        GetMicrosoft365OnboardingStatusCommand request,
        CancellationToken cancellationToken = default)
    {
        var status = await onboardingService.GetStatusAsync(cancellationToken);

        return new GetMicrosoft365OnboardingStatusResponse(
            status.IsAdministrator,
            status.ConnectionStatus,
            status.IsConsentComplete,
            status.HasSelectedSite,
            status.HasIndexedSource,
            status.IsComplete);
    }
}
