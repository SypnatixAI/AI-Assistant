using System.Security.Claims;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Authentication;

namespace AssistantCore.Service.Infrastructure.Authentication;

public sealed class MicrosoftEntraIdentityClaimsMapper : IIdentityClaimsMapper
{
    private static readonly string[] SupportedIssuerPrefixes =
    [
        "https://login.microsoftonline.com/",
        "https://sts.windows.net/"
    ];

    public bool CanMap(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(claim =>
                claim.Type is "tid"
                    or "http://schemas.microsoft.com/identity/claims/tenantid"))
        {
            return true;
        }

        var issuer = principal.FindFirstValue("iss");

        return issuer is not null
            && SupportedIssuerPrefixes.Any(prefix =>
                issuer.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public AuthenticatedIdentity Map(ClaimsPrincipal principal) => new(
        IdentityProvider.MicrosoftEntraId,
        ReadRequiredClaim(
            principal,
            "tid",
            "http://schemas.microsoft.com/identity/claims/tenantid"),
        ReadRequiredClaim(
            principal,
            "oid",
            "http://schemas.microsoft.com/identity/claims/objectidentifier"),
        principal.FindFirstValue("name"),
        principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue(ClaimTypes.Email));

    private static string ReadRequiredClaim(
        ClaimsPrincipal principal,
        params string[] claimTypes)
    {
        var value = claimTypes
            .Select(principal.FindFirstValue)
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UnauthorizedAccessException(
                $"Un claim obligatoire est absent ou invalide. Claims testes: {string.Join(", ", claimTypes)}.");
        }

        return value;
    }
}
