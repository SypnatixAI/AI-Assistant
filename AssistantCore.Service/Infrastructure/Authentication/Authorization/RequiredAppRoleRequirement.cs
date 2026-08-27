using Microsoft.AspNetCore.Authorization;

namespace AssistantCore.Service.Infrastructure.Authentication.Authorization;

public sealed class RequiredAppRoleRequirement(string requiredRole) : IAuthorizationRequirement
{
    public string RequiredRole { get; } = requiredRole;
}
