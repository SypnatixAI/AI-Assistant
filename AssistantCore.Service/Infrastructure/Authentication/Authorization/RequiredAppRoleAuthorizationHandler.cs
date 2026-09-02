using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace AssistantCore.Service.Infrastructure.Authentication.Authorization;

/// <summary>
/// Verifie que le token porte un role Entra autorisant l'admission sur la plateforme.
/// Le role d'admission standard ou tenantAdmin permet l'acces ; tenantAdmin conserve
/// ensuite sa signification de role metier Admin dans la couche Application.
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
        if (HasAcceptedRole(context.User, requirement.AcceptedRoles))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasAcceptedRole(
        ClaimsPrincipal principal,
        IReadOnlyCollection<string> acceptedRoles) =>
        RoleClaimTypes
            .SelectMany(claimType => principal.FindAll(claimType))
            .SelectMany(claim => claim.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(role => acceptedRoles.Contains(role, StringComparer.Ordinal));
}
