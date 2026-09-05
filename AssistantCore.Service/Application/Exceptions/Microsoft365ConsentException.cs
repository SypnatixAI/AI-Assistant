namespace AssistantCore.Service.Application.Exceptions;

public sealed class Microsoft365ConsentException(string message, string errorCode)
    : BadRequestException(message), IErrorCodeException
{
    public const string AdminConsentRefused = "admin_consent_refused";

    public const string AdminConsentIncomplete = "admin_consent_incomplete";

    public const string WrongTenant = "wrong_tenant";

    public const string MissingRequiredPermissions = "missing_required_permissions";

    public const string AdminConsentValidationFailed = "admin_consent_validation_failed";

    public string ErrorCode { get; } = errorCode;
}
