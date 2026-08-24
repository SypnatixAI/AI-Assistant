using AssistantCore.Service.Application.Models.Microsoft365.Permissions;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365PermissionRoleEvaluator
    : IMicrosoft365PermissionRoleEvaluator
{
    private static readonly HashSet<string> DriveItemReadRoles = new(
        ["read", "write", "owner"],
        StringComparer.OrdinalIgnoreCase);

    public Microsoft365PermissionRoleEvaluation EvaluateDriveItemRoles(
        IReadOnlyCollection<string> roles)
    {
        if (roles is null
            || roles.Count == 0
            || roles.Any(role => string.IsNullOrWhiteSpace(role)
                || !DriveItemReadRoles.Contains(role)))
        {
            return Microsoft365PermissionRoleEvaluation.Unresolved;
        }

        return Microsoft365PermissionRoleEvaluation.ReadAllowed;
    }

    public Microsoft365PermissionRoleEvaluation EvaluateSharePointRoleTypes(
        IReadOnlyCollection<int> roleTypeKinds)
    {
        if (roleTypeKinds is null
            || roleTypeKinds.Count == 0
            || roleTypeKinds.Any(roleTypeKind => roleTypeKind is < 1 or > 8))
        {
            return Microsoft365PermissionRoleEvaluation.Unresolved;
        }

        return roleTypeKinds.Any(roleTypeKind => roleTypeKind >= 2)
            ? Microsoft365PermissionRoleEvaluation.ReadAllowed
            : Microsoft365PermissionRoleEvaluation.NoReadAccess;
    }
}
