using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Authorization;

public interface IRolePermissionService
{
    IReadOnlyCollection<Permission> GetPermissions(OrganizationRole role);
}
