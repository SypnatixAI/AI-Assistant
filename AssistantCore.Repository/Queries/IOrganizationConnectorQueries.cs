using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Queries;

public interface IOrganizationConnectorQueries
{
    Task<IReadOnlyCollection<OrganizationConnector>> GetActiveConfiguredConnectors(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
