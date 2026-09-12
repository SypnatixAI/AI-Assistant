using AssistantCore.Service.Infrastructure.Authentication.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Authentication.Authorization;

/// <summary>
/// Fait du scope delegue une exigence de la politique par defaut : tout endpoint annote
/// <c>[Authorize]</c> l'applique sans avoir a repeter un attribut sur chaque controller.
/// </summary>
public sealed class ConfigureApiAuthorizationOptions(IOptions<ApiAccessOptions> apiAccessOptions)
    : IConfigureOptions<AuthorizationOptions>
{
    public void Configure(AuthorizationOptions options)
    {
        var apiAccess = apiAccessOptions.Value;

        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(
                new RequiredScopeRequirement(apiAccess.RequiredScope),
                new RequiredAppRoleRequirement(
                [
                    apiAccess.RequiredAdmissionRole,
                    apiAccess.TenantAdminRole
                ]))
            .Build();

        options.AddPolicy(
            ApiAuthorizationPolicies.ManagementAdmin,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(
                    new RequiredScopeRequirement(apiAccess.RequiredScope),
                    new RequiredAppRoleRequirement(
                        [apiAccess.ManagementAdminRole],
                        apiAccess.ManagementAdminAllowedEmails)));
    }
}
