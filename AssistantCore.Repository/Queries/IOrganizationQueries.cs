using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Queries;

public interface IOrganizationQueries
{
    Task<Organization> GetOrganization(string microsoftTenantId, CancellationToken cancellationToken = default);
}
