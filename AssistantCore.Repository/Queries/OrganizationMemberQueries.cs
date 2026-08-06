using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Queries;

public sealed class OrganizationMemberQueries(AssistantCoreDbContext dbContext) : IOrganizationMemberQueries
{
    public async Task<OrganizationMember?> FindMember(
        Guid organizationId,
        IdentityProvider identityProvider,
        string externalUserId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizationMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                member =>
                    member.OrganizationId == organizationId
                    && member.IdentityProvider == identityProvider
                    && member.ExternalUserId == externalUserId,
                cancellationToken);
    }

    public async Task<OrganizationMember> CreateMember(
        OrganizationMember member,
        CancellationToken cancellationToken = default)
    {
        dbContext.OrganizationMembers.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        return member;
    }
}
