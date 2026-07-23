using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Queries;

public sealed class OrganizationMemberQueries(AssistantCoreDbContext dbContext) : IOrganizationMemberQueries
{
    public async Task<OrganizationMember> GetMember(
        Guid organizationId,
        IdentityProvider identityProvider,
        string externalUserId,
        CancellationToken cancellationToken = default)
    {
        var member = await dbContext.OrganizationMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                member =>
                    member.OrganizationId == organizationId
                    && member.IdentityProvider == identityProvider
                    && member.ExternalUserId == externalUserId
                    && member.Status == RecordStatus.Active,
                cancellationToken);

        return member
            ?? throw new ForbiddenException("Organization member access denied.");
    }
}
