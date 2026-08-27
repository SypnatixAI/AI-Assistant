using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Authentication;

namespace AssistantCore.Service.Application.Services.AuthenticateUser;

public sealed class AuthenticateUserService(
    ICurrentIdentity currentIdentity,
    IOrganizationMemberQueries organizationMemberQueries,
    IOrganizationQueries organizationQueries,
    TimeProvider timeProvider) : IAuthenticateUserService
{
    public async Task<(Organization Organization, OrganizationMember Member)> GetOrganizationAsync(CancellationToken cancellationToken)
    {
        var identity = currentIdentity.GetIdentity();
        var organization = await organizationQueries.FindOrganization(
            identity.Provider,
            identity.ExternalOrganizationId,
            cancellationToken)
            ?? throw new ForbiddenException("Organization access denied.");

        var member = await GetOrCreateActiveMemberAsync(
            organization.Id,
            identity,
            cancellationToken);

        return (organization, member);
    }

    private async Task<OrganizationMember> GetOrCreateActiveMemberAsync(
        Guid organizationId,
        AuthenticatedIdentity identity,
        CancellationToken cancellationToken)
    {
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
                    Role = OrganizationRole.User,
                    Status = RecordStatus.Active
                },
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
