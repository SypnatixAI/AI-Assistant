using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Queries;

public sealed class OrganizationQueries(AssistantCoreDbContext dbContext) : IOrganizationQueries
{
    public async Task<Organization> GetOrganization(
        IdentityProvider identityProvider,
        string externalTenantId,
        CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                organization =>
                    organization.IdentityProvider == identityProvider
                    && organization.ExternalTenantId == externalTenantId
                    && organization.Status == RecordStatus.Active,
                cancellationToken);

        return organization
            ?? throw new ForbiddenException("Organization access denied.");
    }
}
