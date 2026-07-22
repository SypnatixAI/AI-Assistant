using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Queries;

public sealed class OrganizationQueries(AssistantCoreDbContext dbContext) : IOrganizationQueries
{
    public async Task<Organization> GetOrganization(
        string microsoftTenantId,
        CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                organization =>
                    organization.MicrosoftTenantId == microsoftTenantId
                    && organization.Status == RecordStatus.Active,
                cancellationToken);

        return organization
            ?? throw new ForbiddenException("Organization access denied.");
    }
}
