using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace AssistantCore.Service.Infrastructure.Authentication.Authorization;

/// <summary>
/// Verifie que le token porte le scope delegue attendu. Un token applicatif sans utilisateur
/// ne porte aucun scope delegue et se voit donc refuser les endpoints utilisateur.
/// </summary>
public sealed class RequiredScopeAuthorizationHandler
    : AuthorizationHandler<RequiredScopeRequirement>
{
    private static readonly string[] ScopeClaimTypes =
    [
        "scp",
        "http://schemas.microsoft.com/identity/claims/scope"
    ];

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequiredScopeRequirement requirement)
    {
        if (HasRequiredScope(context.User, requirement.RequiredScope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasRequiredScope(ClaimsPrincipal principal, string requiredScope) =>
        ScopeClaimTypes
            .SelectMany(claimType => principal.FindAll(claimType))
            .SelectMany(claim => claim.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(scope => string.Equals(scope, requiredScope, StringComparison.Ordinal));
}
