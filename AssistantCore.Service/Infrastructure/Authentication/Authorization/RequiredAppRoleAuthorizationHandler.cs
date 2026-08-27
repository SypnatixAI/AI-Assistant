using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace AssistantCore.Service.Infrastructure.Authentication.Authorization;

/// <summary>
/// Verifie que le token porte le role d'admission Entra attendu (ex. AssistantCore.Access).
/// Ce role prouve seulement que l'organisation cliente a admis l'utilisateur sur la
/// plateforme ; il ne doit jamais etre mappe vers un role metier interne comme Admin.
/// </summary>
public sealed class RequiredAppRoleAuthorizationHandler
    : AuthorizationHandler<RequiredAppRoleRequirement>
{
    private static readonly string[] RoleClaimTypes =
    [
        "roles",
        ClaimTypes.Role
    ];

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequiredAppRoleRequirement requirement)
    {
        if (HasRequiredRole(context.User, requirement.RequiredRole))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasRequiredRole(ClaimsPrincipal principal, string requiredRole) =>
        RoleClaimTypes
            .SelectMany(claimType => principal.FindAll(claimType))
            .SelectMany(claim => claim.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(role => string.Equals(role, requiredRole, StringComparison.Ordinal));
}
