using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Queries;

public interface IOrganizationMemberQueries
{
    Task<OrganizationMember> GetMember(
        Guid organizationId,
        IdentityProvider identityProvider,
        string externalUserId,
        CancellationToken cancellationToken = default);
}
