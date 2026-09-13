using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Configuration;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.AuthenticateUser;

public sealed class OrganizationRoleResolver(IOptions<OrganizationRoleOptions> options)
    : IOrganizationRoleResolver
{
    public OrganizationRole Resolve(IReadOnlyCollection<string> appRoles)
    {
        var hasAdmission = appRoles.Contains(
            options.Value.RequiredAdmissionRole,
            StringComparer.Ordinal);

        var hasTenantAdmin = appRoles.Contains(
            options.Value.TenantAdminRole,
            StringComparer.Ordinal);

        if (!hasAdmission && !hasTenantAdmin)
        {
            throw new ForbiddenException("Organization member access denied.");
        }

        return hasTenantAdmin ? OrganizationRole.Admin : OrganizationRole.User;
    }
}
