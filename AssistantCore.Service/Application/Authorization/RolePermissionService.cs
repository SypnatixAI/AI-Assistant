using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Authorization;

public sealed class RolePermissionService : IRolePermissionService
{
    public IReadOnlyCollection<Permission> GetPermissions(OrganizationRole role) =>
        role switch
        {
            OrganizationRole.TenantAdmin =>
            [
                Permission.OrganizationRead,
                Permission.OrganizationManage,
                Permission.MemberRead,
                Permission.MemberManage,
                Permission.ConnectorRead,
                Permission.ConnectorManage,
                Permission.AuditRead,
                Permission.SearchUse
            ],
            OrganizationRole.Manager =>
            [
                Permission.OrganizationRead,
                Permission.MemberRead,
                Permission.ConnectorRead,
                Permission.AuditRead,
                Permission.SearchUse
            ],
            OrganizationRole.User =>
            [
                Permission.OrganizationRead,
                Permission.SearchUse
            ],
            _ => throw new InvalidOperationException($"Organization role '{role}' is not supported.")
        };
}
