using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Queries;

public interface IOrganizationMemberQueries
{
    Task<IReadOnlyCollection<OrganizationMember>> GetMembers(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<OrganizationMember?> FindMember(
        Guid organizationId,
        IdentityProvider identityProvider,
        string externalUserId,
        CancellationToken cancellationToken = default);

    Task<OrganizationMember?> FindMember(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken = default);

    Task<OrganizationMember> CreateMember(
        OrganizationMember member,
        CancellationToken cancellationToken = default);

    Task<OrganizationMember> UpdateRole(
        OrganizationMember member,
        OrganizationRole role,
        CancellationToken cancellationToken = default);

    Task RecordSuccessfulAuthenticationAsync(
        Guid memberId,
        DateTimeOffset authenticatedAt,
        CancellationToken cancellationToken = default);
}
