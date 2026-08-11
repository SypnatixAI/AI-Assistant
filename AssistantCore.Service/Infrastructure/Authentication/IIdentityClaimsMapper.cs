using System.Security.Claims;
using AssistantCore.Service.Application.Models.Authentication;

namespace AssistantCore.Service.Infrastructure.Authentication;

public interface IIdentityClaimsMapper
{
    bool CanMap(ClaimsPrincipal principal);

    AuthenticatedIdentity Map(ClaimsPrincipal principal);
}
