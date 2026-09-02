using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Services.Microsoft365;
using AssistantCore.Service.Application.Services.TenantAdmission;

namespace AssistantCore.Service.Application.Services.Messages.Authorization;

public sealed class MessageUserContextService(
    ICurrentIdentity currentIdentity,
    IOrganizationQueries organizationQueries,
    IOrganizationMemberQueries memberQueries,
    IOrganizationRoleResolver organizationRoleResolver,
    IMicrosoft365OnboardingCompletionChecker onboardingCompletionChecker,
    ITenantAdmissionPolicy tenantAdmissionPolicy) : IMessageUserContextService
{
    public async Task<MessageUserContext> GetCurrentAsync(
        CancellationToken cancellationToken)
    {
        var identity = currentIdentity.GetIdentity();
        var organization = await organizationQueries.FindOrganization(
            identity.Provider,
            identity.ExternalOrganizationId,
            cancellationToken);

        if (organization is null || organization.Status != RecordStatus.Active)
        {
            throw new ForbiddenException("Organization access denied.");
        }

        var member = await memberQueries.FindMember(
            organization.Id,
            identity.Provider,
            identity.ExternalUserId,
            cancellationToken);

        if (member is null || member.Status != RecordStatus.Active)
        {
            throw new ForbiddenException("Organization member access denied.");
        }

        // Le rôle effectif vient du JWT; la valeur persistée reste informative.
        member.Role = organizationRoleResolver.Resolve(identity.AppRoles);

        var isOnboardingComplete = await onboardingCompletionChecker.IsCompleteAsync(
            organization.Id,
            cancellationToken);
        var admissionResult = tenantAdmissionPolicy.Evaluate(member.Role, isOnboardingComplete);

        if (admissionResult != TenantAdmissionResult.Allowed)
        {
            throw new TenantAdmissionException(
                "A tenant administrator must finish the Microsoft 365 setup before other members can access AssistantCore.",
                TenantAdmissionException.TenantAdminRequired);
        }

        return new MessageUserContext(organization, member);
    }
}
