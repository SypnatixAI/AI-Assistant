using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Queries;

public sealed class OrganizationMemberQueries(AssistantCoreDbContext dbContext) : IOrganizationMemberQueries
{
    public async Task<OrganizationMember> GetMember(
        string microsoftIdentifier,
        CancellationToken cancellationToken = default)
    {
        var member = await dbContext.OrganizationMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                member => member.MicrosoftIdentifier == microsoftIdentifier,
                cancellationToken);

        return member
            ?? throw new ForbiddenException("Organization member access denied.");
    }
}
