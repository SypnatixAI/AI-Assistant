using System.Security.Claims;
using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Abstractions;

public sealed class CurrentIdentity : ICurrentIdentity
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentIdentity(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal User =>
        _httpContextAccessor.HttpContext?.User
        ?? throw new UnauthorizedAccessException(
            "Aucun utilisateur authentifié.");

    public IdentityProvider IdentityProvider => IdentityProvider.MicrosoftEntraId;

    public string ExternalTenantId =>
        ReadRequiredClaim("tid", "http://schemas.microsoft.com/identity/claims/tenantid");

    public string ExternalUserId =>
        ReadRequiredClaim("oid", "http://schemas.microsoft.com/identity/claims/objectidentifier");

    public string? DisplayName =>
        User.FindFirstValue("name");

    public string? Email =>
        User.FindFirstValue("preferred_username")
        ?? User.FindFirstValue(ClaimTypes.Email);

    private string ReadRequiredClaim(params string[] claimTypes)
    {
        var value = claimTypes
            .Select(User.FindFirstValue)
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UnauthorizedAccessException(
                $"Un claim obligatoire est absent ou invalide. Claims testes: {string.Join(", ", claimTypes)}.");
        }

        return value;
    }
}
