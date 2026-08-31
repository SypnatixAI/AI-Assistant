using AssistantCore.Repository.Abstractions;

namespace AssistantCore.Service.Application.Exceptions;

public sealed class TenantAdmissionException(string message, string errorCode)
    : ForbiddenException(message), IErrorCodeException
{
    public const string TenantAdminRequired = "tenant_admin_required";

    public string ErrorCode { get; } = errorCode;
}
