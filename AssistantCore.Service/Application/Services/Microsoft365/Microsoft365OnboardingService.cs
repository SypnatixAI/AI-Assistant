using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.AuthenticateUser;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365OnboardingService(
    IAuthenticateUserService authenticateUserService,
    IMicrosoft365ConnectionRepository connectionRepository)
    : IMicrosoft365OnboardingService
{
    public async Task<Microsoft365OnboardingStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var (organization, member) =
            await authenticateUserService.GetOrganizationAsync(cancellationToken);
        var connection = await connectionRepository.FindByOrganizationAsync(
            organization.Id,
            cancellationToken);
        var isActive = connection?.Status == Microsoft365ConnectionStatus.Active;

        return new Microsoft365OnboardingStatus(
            member.Role == OrganizationRole.Admin,
            connection?.Status.ToString() ?? "NotStarted",
            IsConsentComplete: isActive,
            HasSelectedSite: isActive,
            HasIndexedSource: isActive,
            IsEnvironmentReady: isActive);
    }
}
