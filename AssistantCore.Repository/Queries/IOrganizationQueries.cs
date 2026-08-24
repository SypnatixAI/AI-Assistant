using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Queries;

public interface IOrganizationQueries
{
    Task<Organization?> FindOrganization(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<Organization?> FindOrganization(
        IdentityProvider identityProvider,
        string externalTenantId,
        CancellationToken cancellationToken = default);
}
