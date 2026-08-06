using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Queries;

public interface IOrganizationMemberQueries
{
    Task<OrganizationMember?> FindMember(
        Guid organizationId,
        IdentityProvider identityProvider,
        string externalUserId,
        CancellationToken cancellationToken = default);

    Task<OrganizationMember> CreateMember(
        OrganizationMember member,
        CancellationToken cancellationToken = default);
}
