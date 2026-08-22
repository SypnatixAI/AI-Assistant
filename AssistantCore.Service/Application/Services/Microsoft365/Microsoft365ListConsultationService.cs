using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.AuthenticateUser;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365ListConsultationService(
    IAuthenticateUserService authenticateUserService,
    IMicrosoft365SourceDiscoveryRepository sourceDiscoveryRepository)
    : IMicrosoft365ListConsultationService
{
    public async Task<IReadOnlyCollection<Microsoft365List>> GetListsAsync(
        string siteId,
        CancellationToken cancellationToken = default)
    {
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);
        if (member.Role != OrganizationRole.Admin)
        {
            throw new ForbiddenException("Administrator access required.");
        }

        _ = await sourceDiscoveryRepository.FindSiteAsync(
            organization.Id,
            siteId,
            cancellationToken)
            ?? throw new NotFoundException("Microsoft 365 site was not found.");

        return await sourceDiscoveryRepository.GetListsAsync(
            organization.Id,
            siteId,
            cancellationToken);
    }
}
