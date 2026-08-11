using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Authentication;

namespace AssistantCore.Service.Infrastructure.Authentication;

public sealed class HttpCurrentIdentity(
    IHttpContextAccessor httpContextAccessor,
    IEnumerable<IIdentityClaimsMapper> claimsMappers) : ICurrentIdentity
{
    private AuthenticatedIdentity? _identity;

    public AuthenticatedIdentity GetIdentity()
    {
        if (_identity is not null)
        {
            return _identity;
        }

        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("Aucun utilisateur authentifie.");
        }

        var matchingMappers = claimsMappers
            .Where(mapper => mapper.CanMap(principal))
            .Take(2)
            .ToArray();

        if (matchingMappers.Length != 1)
        {
            throw new UnauthorizedAccessException(
                "Le fournisseur d'identite est absent, non supporte ou ambigu.");
        }

        _identity = matchingMappers[0].Map(principal);
        return _identity;
    }
}
