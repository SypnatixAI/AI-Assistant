using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365OnboardingCompletionChecker(
    IMicrosoft365ConnectionRepository connectionRepository,
    IMicrosoft365SourceDiscoveryRepository sourceRepository)
    : IMicrosoft365OnboardingCompletionChecker
{
    public async Task<bool> IsCompleteAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionRepository.FindByOrganizationAsync(
            organizationId,
            cancellationToken);

        if (connection is null || connection.Status != Microsoft365ConnectionStatus.Active)
        {
            return false;
        }

        var selectedSiteIds = await sourceRepository.GetSiteIdsAsync(
            organizationId,
            cancellationToken);

        return selectedSiteIds.Count > 0;
    }
}
