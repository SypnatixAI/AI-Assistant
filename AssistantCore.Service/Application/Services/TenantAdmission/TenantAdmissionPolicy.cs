using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Services.TenantAdmission;

public sealed class TenantAdmissionPolicy : ITenantAdmissionPolicy
{
    public TenantAdmissionResult Evaluate(OrganizationRole memberRole, bool isOnboardingComplete)
    {
        if (isOnboardingComplete || memberRole == OrganizationRole.Admin)
        {
            return TenantAdmissionResult.Allowed;
        }

        return TenantAdmissionResult.TenantAdminRequired;
    }
}
