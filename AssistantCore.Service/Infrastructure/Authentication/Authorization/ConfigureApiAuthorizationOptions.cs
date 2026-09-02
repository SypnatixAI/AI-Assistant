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
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(
                new RequiredScopeRequirement(apiAccessOptions.Value.RequiredScope),
                new RequiredAppRoleRequirement(
                [
                    apiAccessOptions.Value.RequiredAdmissionRole,
                    apiAccessOptions.Value.TenantAdminRole
                ]))
            .Build();
    }
}
