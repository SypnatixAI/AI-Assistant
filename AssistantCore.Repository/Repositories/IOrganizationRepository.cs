using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> TryCreateOrganizationAsync(
        Organization organization,
        CancellationToken cancellationToken = default);

    Task<Organization?> AssociateExternalTenantIdAsync(
        Guid organizationId,
        IdentityProvider identityProvider,
        string externalTenantId,
        CancellationToken cancellationToken = default);
}
