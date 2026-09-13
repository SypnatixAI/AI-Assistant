using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Services.Members;

public interface IMemberManagementService
{
    Task<IReadOnlyCollection<OrganizationMember>> GetMembersAsync(
        CancellationToken cancellationToken = default);

    Task<OrganizationMember> UpdateMemberRoleAsync(
        Guid memberId,
        string role,
        CancellationToken cancellationToken = default);

    Task<OrganizationMember> UpdateMemberStatusAsync(
        Guid memberId,
        string status,
        int? expectedVersion,
        CancellationToken cancellationToken = default);
}
