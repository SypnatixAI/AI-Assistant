using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Authentication;

namespace AssistantCore.Service.Application.Services.AuthenticateUser;

public sealed class AuthenticateUserService(
    ICurrentIdentity currentIdentity,
    IOrganizationMemberQueries organizationMemberQueries,
    IOrganizationQueries organizationQueries,
    IOrganizationRepository organizationRepository,
    IOrganizationRoleResolver organizationRoleResolver,
    TimeProvider timeProvider) : IAuthenticateUserService
{
    public async Task<(Organization Organization, OrganizationMember Member)> GetOrganizationAsync(CancellationToken cancellationToken)
    {
        var identity = currentIdentity.GetIdentity();
        var organization = await organizationQueries.FindOrganization(
            identity.Provider,
            identity.ExternalOrganizationId,
            cancellationToken)
            ?? await ResolveOrganizationFromEmailDomainAsync(identity, cancellationToken)
            ?? throw new ForbiddenException(
                $"No active organization is registered for tenant '{identity.ExternalOrganizationId}'.");

        var member = await GetOrCreateActiveMemberAsync(
            organization.Id,
            identity,
            cancellationToken);

        return (organization, member);
    }

    private async Task<Organization?> ResolveOrganizationFromEmailDomainAsync(
        AuthenticatedIdentity identity,
        CancellationToken cancellationToken)
    {
        var email = ResolveEmail(identity);
        var separatorIndex = email.LastIndexOf('@');
        if (separatorIndex < 0 || separatorIndex == email.Length - 1)
        {
            return null;
        }

        var domain = email[(separatorIndex + 1)..].Trim().ToLowerInvariant();
        var organization = await organizationQueries.FindOrganizationByDomain(
            identity.Provider,
            domain,
            cancellationToken);

        if (organization is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(organization.ExternalTenantId))
        {
            return organization;
        }

        return await organizationRepository.AssociateExternalTenantIdAsync(
            organization.Id,
            identity.Provider,
            identity.ExternalOrganizationId,
            cancellationToken);
    }

    private async Task<OrganizationMember> GetOrCreateActiveMemberAsync(
        Guid organizationId,
        AuthenticatedIdentity identity,
        CancellationToken cancellationToken)
    {
        var resolvedRole = organizationRoleResolver.Resolve(identity.AppRoles);
        var member = await organizationMemberQueries.FindMember(
            organizationId,
            identity.Provider,
            identity.ExternalUserId,
            cancellationToken);

        if (member is null)
        {
            member = await organizationMemberQueries.CreateMember(
                new OrganizationMember
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Name = ResolveDisplayName(identity),
                    Email = ResolveEmail(identity),
                    IdentityProvider = identity.Provider,
                    ExternalUserId = identity.ExternalUserId,
                    Role = resolvedRole,
                    Status = RecordStatus.Active
                },
                cancellationToken);
        }
        else if (member.Role != resolvedRole)
        {
            member = await organizationMemberQueries.UpdateRole(
                member,
                resolvedRole,
                cancellationToken);
        }

        if (member.Status != RecordStatus.Active)
        {
            throw new ForbiddenException("Organization member access denied.");
        }

        await organizationMemberQueries.RecordSuccessfulAuthenticationAsync(
            member.Id,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return member;
    }

    private static string ResolveEmail(AuthenticatedIdentity identity) =>
        !string.IsNullOrWhiteSpace(identity.Email)
            ? identity.Email
            : throw new UnauthorizedAccessException("Authenticated user email is missing.");

    private static string ResolveDisplayName(AuthenticatedIdentity identity)
    {
        if (!string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            return identity.DisplayName;
        }

        return ResolveEmail(identity);
    }
}
