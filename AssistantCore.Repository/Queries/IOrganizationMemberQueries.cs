using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Queries;

public interface IOrganizationMemberQueries
{
    Task<OrganizationMember> GetMember(string microsoftIdentifier, CancellationToken cancellationToken = default);
}
