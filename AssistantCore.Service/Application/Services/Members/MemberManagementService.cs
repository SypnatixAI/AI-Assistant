using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Members;

public sealed class MemberManagementService(
    IAuthenticateUserService authenticateUserService,
    IOrganizationMemberQueries organizationMemberQueries,
    ICurrentIdentity currentIdentity,
    IOptions<OrganizationRoleOptions> organizationRoleOptions) : IMemberManagementService
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

    public async Task<OrganizationMember> UpdateMemberStatusAsync(
        Guid memberId,
        string status,
        int? expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var (organization, currentMember) = await GetTenantAdminContextAsync(cancellationToken);

        if (memberId == Guid.Empty)
        {
            throw new BadRequestException("Member identifier is required.");
        }

        var newStatus = ParseStatus(status);

        if (currentMember.Id == memberId)
        {
            throw new BadRequestException("An administrator cannot change their own status.");
        }

        var result = await organizationMemberQueries.UpdateStatus(
            organization.Id,
            memberId,
            newStatus,
            expectedVersion,
            cancellationToken);

        return result.Status switch
        {
            MemberUpdateStatus.Updated when result.Member is not null => result.Member,
            MemberUpdateStatus.VersionConflict => throw new ConflictException(
                "The organization member was modified in another session.",
                ConflictException.MemberVersionConflict),
            _ => throw new NotFoundException("Organization member not found.")
        };
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

    private async Task<(Organization Organization, OrganizationMember CurrentMember)> GetTenantAdminContextAsync(
        CancellationToken cancellationToken)
    {
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);
        var identity = currentIdentity.GetIdentity();

        if (!identity.AppRoles.Contains(organizationRoleOptions.Value.TenantAdminRole, StringComparer.Ordinal))
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

    private static RecordStatus ParseStatus(string status) =>
        status switch
        {
            nameof(RecordStatus.Active) => RecordStatus.Active,
            nameof(RecordStatus.Inactive) => RecordStatus.Inactive,
            _ => throw new BadRequestException("Status must be 'Active' or 'Inactive'.")
        };
}
