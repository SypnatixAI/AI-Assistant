using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Services.Organizations;

public interface IOrganizationManagementService
{
    Task<Organization> CreateOrganizationAsync(
        string name,
        string identityProvider,
        string externalTenantId,
        CancellationToken cancellationToken = default);
}
