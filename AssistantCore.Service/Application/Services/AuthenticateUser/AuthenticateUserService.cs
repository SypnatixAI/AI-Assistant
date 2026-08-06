using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Abstractions;

namespace AssistantCore.Service.Application.Services.AuthenticateUser;

public sealed class AuthenticateUserService(
    ICurrentIdentity currentIdentity,
    IOrganizationMemberQueries organizationMemberQueries,
    IOrganizationQueries organizationQueries) : IAuthenticateUserService
{
    public async Task<(Organization Organization, OrganizationMember Member)> GetOrganizationAsync(CancellationToken cancellationToken)
    {
        var organization = await organizationQueries.FindOrganization(
            currentIdentity.IdentityProvider,
            currentIdentity.ExternalTenantId,
            cancellationToken)
            ?? throw new ForbiddenException("Organization access denied.");

        var member = await GetOrCreateActiveMemberAsync(organization.Id, cancellationToken);

        return (organization, member);
    }

    private async Task<OrganizationMember> GetOrCreateActiveMemberAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var member = await organizationMemberQueries.FindMember(
            organizationId,
            currentIdentity.IdentityProvider,
            currentIdentity.ExternalUserId,
            cancellationToken);

        if (member is null)
        {
            member = await organizationMemberQueries.CreateMember(
                new OrganizationMember
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Name = ResolveDisplayName(),
                    Email = ResolveEmail(),
                    IdentityProvider = currentIdentity.IdentityProvider,
                    ExternalUserId = currentIdentity.ExternalUserId,
                    Role = OrganizationRole.User,
                    Status = RecordStatus.Active
                },
                cancellationToken);
        }

        if (member.Status != RecordStatus.Active)
        {
            throw new ForbiddenException("Organization member access denied.");
        }

        return member;
    }

    private string ResolveEmail() =>
        !string.IsNullOrWhiteSpace(currentIdentity.Email)
            ? currentIdentity.Email
            : throw new UnauthorizedAccessException("Authenticated user email is missing.");

    private string ResolveDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(currentIdentity.DisplayName))
        {
            return currentIdentity.DisplayName;
        }

        return ResolveEmail();
    }
}
