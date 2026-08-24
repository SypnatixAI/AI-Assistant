using AssistantCore.Service.Application.Models.Microsoft365.Permissions;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365PermissionRoleEvaluator
{
    Microsoft365PermissionRoleEvaluation EvaluateDriveItemRoles(
        IReadOnlyCollection<string> roles);

    Microsoft365PermissionRoleEvaluation EvaluateSharePointRoleTypes(
        IReadOnlyCollection<int> roleTypeKinds);
}
