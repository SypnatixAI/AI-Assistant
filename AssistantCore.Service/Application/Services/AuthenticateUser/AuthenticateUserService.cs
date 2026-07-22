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
            currentIdentity.TenantId.ToString(), 
            cancellationToken);

        var member = await organizationMemberQueries.GetMember(
            currentIdentity.ObjectId.ToString(),
            cancellationToken);

        return (organization, member);
    }
}