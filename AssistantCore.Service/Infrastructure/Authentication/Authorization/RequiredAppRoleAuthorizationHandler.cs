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

    private static readonly string[] EmailClaimTypes =
    [
        "preferred_username",
        "email",
        "upn",
        "unique_name",
        ClaimTypes.Email
    ];

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequiredAppRoleRequirement requirement)
    {
        if (HasAcceptedRole(context.User, requirement.AcceptedRoles)
            || HasAcceptedEmail(context.User, requirement.AcceptedEmails))
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

    private static bool HasAcceptedEmail(
        ClaimsPrincipal principal,
        IReadOnlyCollection<string> acceptedEmails) =>
        acceptedEmails.Count > 0
        && EmailClaimTypes
            .SelectMany(principal.FindAll)
            .Select(claim => claim.Value)
            .Any(email => acceptedEmails.Contains(email, StringComparer.OrdinalIgnoreCase));
}
