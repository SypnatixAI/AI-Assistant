using AssistantCore.Service.Infrastructure.Authentication.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Authentication.Authorization;

/// <summary>
/// Politique dediee a l'endpoint interne de gestion de la politique de quota. Elle exige
/// uniquement le role applicatif dedie, jamais le scope delegue : un token utilisateur
/// avec access_as_user ne porte pas ce role et se voit donc refuser l'acces.
/// </summary>
public sealed class ConfigureUsagePolicyAuthorizationOptions(IOptions<ApiAccessOptions> apiAccessOptions)
    : IConfigureOptions<AuthorizationOptions>
{
    public void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(
            UsagePolicyAuthorizationPolicies.Management,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(
                    new RequiredAppRoleRequirement(apiAccessOptions.Value.UsagePolicyManagementRole)));
    }
}
