using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Queries;

public sealed class OrganizationMemberQueries(AssistantCoreDbContext dbContext) : IOrganizationMemberQueries
{
    public async Task<IReadOnlyCollection<OrganizationMember>> GetMembers(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(member => member.OrganizationId == organizationId)
            .OrderBy(member => member.Name)
            .ThenBy(member => member.Email)
            .ToListAsync(cancellationToken);
    }

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

    public async Task<OrganizationMember?> FindMember(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizationMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                member =>
                    member.OrganizationId == organizationId
                    && member.Id == memberId,
                cancellationToken);
    }

    public async Task<OrganizationMember> CreateMember(
        OrganizationMember member,
        CancellationToken cancellationToken = default)
    {
        dbContext.OrganizationMembers.Add(member);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return member;
        }
        catch (DbUpdateException exception) when (IsMemberIdentityConflict(exception))
        {
            dbContext.Entry(member).State = EntityState.Detached;

            var competingMember = await FindMember(
                member.OrganizationId,
                member.IdentityProvider,
                member.ExternalUserId,
                cancellationToken);

            return competingMember
                ?? throw new InvalidOperationException(
                    "The member created by the competing request could not be reloaded.");
        }
    }

    private static bool IsMemberIdentityConflict(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;

        return message.Contains(
                   "IX_OrganizationMember_OrganizationId_IdentityProvider_ExternalUserId",
                   StringComparison.OrdinalIgnoreCase)
               || (message.Contains(nameof(OrganizationMember.OrganizationId), StringComparison.OrdinalIgnoreCase)
                   && message.Contains(nameof(OrganizationMember.IdentityProvider), StringComparison.OrdinalIgnoreCase)
                   && message.Contains(nameof(OrganizationMember.ExternalUserId), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<OrganizationMember> UpdateRole(
        OrganizationMember member,
        OrganizationRole role,
        CancellationToken cancellationToken = default)
    {
        if (member.Role == role)
        {
            return member;
        }

        dbContext.OrganizationMembers.Attach(member);
        member.Role = role;
        await dbContext.SaveChangesAsync(cancellationToken);
        return member;
    }

    public async Task RecordSuccessfulAuthenticationAsync(
        Guid memberId,
        DateTimeOffset authenticatedAt,
        CancellationToken cancellationToken = default)
    {
        var member = await dbContext.OrganizationMembers
            .SingleOrDefaultAsync(candidate => candidate.Id == memberId, cancellationToken);

        if (member is null || member.LastSuccessfulAuthenticationAt >= authenticatedAt)
        {
            return;
        }

        member.LastSuccessfulAuthenticationAt = authenticatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
