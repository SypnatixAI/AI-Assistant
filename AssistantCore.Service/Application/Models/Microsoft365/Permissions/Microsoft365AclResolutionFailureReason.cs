namespace AssistantCore.Service.Application.Models.Microsoft365.Permissions;

public enum Microsoft365AclResolutionFailureReason
{
    UnknownPrincipal,
    PartialResponse,
    AccessDenied,
    Timeout,
    UnsupportedPermission
}
