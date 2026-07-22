using System.Security.Claims;

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

    public Guid TenantId =>
        ReadRequiredGuidClaim("tid", "http://schemas.microsoft.com/identity/claims/tenantid");

    public Guid ObjectId =>
        ReadRequiredGuidClaim("oid", "http://schemas.microsoft.com/identity/claims/objectidentifier");

    public string? DisplayName =>
        User.FindFirstValue("name");

    public string? Email =>
        User.FindFirstValue("preferred_username")
        ?? User.FindFirstValue(ClaimTypes.Email);

    private Guid ReadRequiredGuidClaim(params string[] claimTypes)
    {
        var value = claimTypes
            .Select(User.FindFirstValue)
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

        if (!Guid.TryParse(value, out var result))
        {
            throw new UnauthorizedAccessException(
                $"Un claim GUID obligatoire est absent ou invalide. Claims testes: {string.Join(", ", claimTypes)}.");
        }

        return result;
    }
}
