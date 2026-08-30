using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365OnboardingService
{
    Task<Microsoft365OnboardingStatus> GetStatusAsync(
        CancellationToken cancellationToken = default);
}
