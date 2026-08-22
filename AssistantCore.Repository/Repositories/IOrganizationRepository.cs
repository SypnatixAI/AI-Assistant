using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> TryCreateOrganizationAsync(
        Organization organization,
        CancellationToken cancellationToken = default);
}
