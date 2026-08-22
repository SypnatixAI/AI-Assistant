using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.AuthenticateUser;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365SiteSourcesDiscoveryService(
    IAuthenticateUserService authenticateUserService,
    IMicrosoft365SourceDiscoveryRepository sourceDiscoveryRepository,
    IMicrosoft365SiteSourcesClient siteSourcesClient,
    IMicrosoft365TechnicalTokenStore tokenStore,
    TimeProvider timeProvider) : IMicrosoft365SiteSourcesDiscoveryService
{
    public async Task<Microsoft365SiteSourcesDiscoveryResult> DiscoverAsync(
        string siteId,
        CancellationToken cancellationToken = default)
    {
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);
        if (member.Role != OrganizationRole.Admin)
        {
            throw new ForbiddenException("Administrator access required.");
        }

        var site = await sourceDiscoveryRepository.FindSiteAsync(
            organization.Id,
            siteId,
            cancellationToken)
            ?? throw new NotFoundException("Microsoft 365 site was not found.");

        if (site.Microsoft365Connection.Status != Microsoft365ConnectionStatus.Active
            || site.OrganizationConnector.Status != RecordStatus.Active
            || !site.OrganizationConnector.IsConfigured)
        {
            throw new BadRequestException("Microsoft 365 connector is not active.");
        }

        if (site.Status != Microsoft365SourceStatus.Enabled)
        {
            throw new BadRequestException("Microsoft 365 site is not enabled.");
        }

        var accessToken = await tokenStore.GetAsync(
            site.Microsoft365ConnectionId,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new Microsoft365ExternalException("Microsoft 365 technical token is unavailable.");
        }

        var result = await siteSourcesClient.GetSiteSourcesAsync(
            accessToken,
            site.SiteId,
            cancellationToken);
        if (result.Status != Microsoft365SiteSourcesDiscoveryStatus.Succeeded)
        {
            return result;
        }

        await sourceDiscoveryRepository.ReconcileSiteSourcesAsync(
            site,
            result.Sources.Drives
                .Select(drive => new Microsoft365SourceDiscoveryData(
                    drive.DriveId,
                    drive.DisplayName,
                    drive.WebUrl))
                .ToArray(),
            result.Sources.Lists
                .Select(list => new Microsoft365SourceDiscoveryData(
                    list.ListId,
                    list.DisplayName,
                    list.WebUrl))
                .ToArray(),
            timeProvider.GetUtcNow(),
            cancellationToken);

        return result;
    }
}
