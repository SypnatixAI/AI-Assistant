using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.AuthenticateUser;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365SiteDiscoveryService(
    IAuthenticateUserService authenticateUserService,
    IMicrosoft365ConnectionRepository connectionRepository,
    IMicrosoft365SourceDiscoveryRepository sourceRepository,
    IMicrosoft365SiteClient siteClient) : IMicrosoft365SiteDiscoveryService
{
    public async Task<IReadOnlyCollection<Microsoft365AvailableSiteResponse>> GetAvailableSitesAsync(
        CancellationToken cancellationToken = default)
    {
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);
        if (member.Role != OrganizationRole.Admin)
        {
            throw new ForbiddenException("Administrator access required.");
        }

        var connection = await connectionRepository.FindActiveByOrganizationAsync(
            organization.Id,
            cancellationToken) ?? throw new NotFoundException("Active Microsoft 365 connection was not found.");
        var availableSites = await siteClient.ListAsync(
            connection.TenantId!,
            cancellationToken);
        var selectedSiteIds = await sourceRepository.GetSiteIdsAsync(
            organization.Id,
            cancellationToken);
        var selectedSiteIdSet = selectedSiteIds.ToHashSet(StringComparer.Ordinal);

        return availableSites
            .OrderBy(site => site.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(site => new Microsoft365AvailableSiteResponse(
                site.SiteId,
                site.DisplayName,
                site.WebUrl,
                selectedSiteIdSet.Contains(site.SiteId)))
            .ToArray();
    }
}
