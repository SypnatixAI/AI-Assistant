using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Abstractions;

namespace AssistantCore.Service.Application.Services.AuthenticateUser;

public sealed class AuthenticateUserService(
    ICurrentIdentity currentIdentity,
    IOrganizationMemberQueries organizationMemberQueries,
    IOrganizationQueries organizationQueries) : IAuthenticateUserService
{
    public async Task<(Organization Organization, OrganizationMember Member)> GetOrganizationAsync(CancellationToken cancellationToken)
    {
        var organization = await organizationQueries.GetOrganization(
            currentIdentity.IdentityProvider,
            currentIdentity.ExternalTenantId,
            cancellationToken);

        var member = await organizationMemberQueries.GetMember(
            organization.Id,
            currentIdentity.IdentityProvider,
            currentIdentity.ExternalUserId,
            cancellationToken);

        return (organization, member);
    }
}
