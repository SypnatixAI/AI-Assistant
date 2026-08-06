using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.AuthenticateUser;

namespace AssistantCore.Service.Application.Services.Members;

public sealed class MemberManagementService(
    IAuthenticateUserService authenticateUserService,
    IOrganizationMemberQueries organizationMemberQueries) : IMemberManagementService
{
    public async Task<IReadOnlyCollection<OrganizationMember>> GetMembersAsync(
        CancellationToken cancellationToken = default)
    {
        var (organization, _) = await GetAdminContextAsync(cancellationToken);
        return await organizationMemberQueries.GetMembers(organization.Id, cancellationToken);
    }

    public async Task<OrganizationMember> UpdateMemberRoleAsync(
        Guid memberId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var (organization, currentAdmin) = await GetAdminContextAsync(cancellationToken);

        if (memberId == Guid.Empty)
        {
            throw new BadRequestException("Member identifier is required.");
        }

        var newRole = ParseRole(role);

        if (currentAdmin.Id == memberId)
        {
            throw new BadRequestException("An administrator cannot change their own role.");
        }

        var member = await organizationMemberQueries.FindMember(
            organization.Id,
            memberId,
            cancellationToken)
            ?? throw new NotFoundException("Organization member not found.");

        if (member.Status != RecordStatus.Active)
        {
            throw new BadRequestException("An inactive organization member role cannot be changed.");
        }

        return await organizationMemberQueries.UpdateRole(member, newRole, cancellationToken);
    }

    private async Task<(Organization Organization, OrganizationMember Admin)> GetAdminContextAsync(
        CancellationToken cancellationToken)
    {
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);

        if (member.Role != OrganizationRole.Admin)
        {
            throw new ForbiddenException("Administrator access required.");
        }

        return (organization, member);
    }

    private static OrganizationRole ParseRole(string role) =>
        role switch
        {
            nameof(OrganizationRole.Admin) => OrganizationRole.Admin,
            nameof(OrganizationRole.User) => OrganizationRole.User,
            _ => throw new BadRequestException("Role must be 'Admin' or 'User'.")
        };
}
