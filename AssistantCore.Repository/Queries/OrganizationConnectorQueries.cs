using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Queries;

public sealed class OrganizationConnectorQueries(AssistantCoreDbContext dbContext)
    : IOrganizationConnectorQueries
{
    public async Task<IReadOnlyCollection<OrganizationConnector>> GetActiveConfiguredConnectors(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizationConnectors
            .AsNoTracking()
            .Where(connector =>
                connector.OrganizationId == organizationId
                && connector.Status == RecordStatus.Active
                && connector.IsConfigured)
            .Include(connector => connector.Sources.Where(source =>
                source.Status == RecordStatus.Active
                && source.IsIndexed))
            .OrderBy(connector => connector.Type)
            .ToArrayAsync(cancellationToken);
    }
}
