using Microsoft.AspNetCore.Authorization;

namespace AssistantCore.Service.Infrastructure.Authentication.Authorization;

public sealed class RequiredAppRoleRequirement : IAuthorizationRequirement
{
    public RequiredAppRoleRequirement(string requiredRole)
        : this([requiredRole], [])
    {
    }

    public RequiredAppRoleRequirement(IReadOnlyCollection<string> acceptedRoles)
        : this(acceptedRoles, [])
    {
    }

    public RequiredAppRoleRequirement(
        IReadOnlyCollection<string> acceptedRoles,
        IReadOnlyCollection<string> acceptedEmails)
    {
        if (acceptedRoles.Count == 0)
        {
            throw new ArgumentException("At least one accepted role is required.", nameof(acceptedRoles));
        }

        AcceptedRoles = acceptedRoles;
        AcceptedEmails = acceptedEmails;
    }

    public IReadOnlyCollection<string> AcceptedRoles { get; }

    public IReadOnlyCollection<string> AcceptedEmails { get; }
}
