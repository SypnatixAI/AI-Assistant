using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Services.AuthenticateUser;

namespace AssistantCore.Service.Tests;

internal sealed class StubCurrentIdentity : ICurrentIdentity
{
    public IdentityProvider IdentityProvider { get; init; } = IdentityProvider.MicrosoftEntraId;

    public string ExternalTenantId { get; init; } = "tenant-id";

    public string ExternalUserId { get; init; } = "user-id";

    public string? DisplayName { get; init; } = "Test User";

    public string? Email { get; init; } = "test.user@example.com";
}

internal sealed class StubOrganizationQueries : IOrganizationQueries
{
    public Organization? Result { get; set; }

    public IdentityProvider? ReceivedIdentityProvider { get; private set; }

    public string? ReceivedExternalTenantId { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<Organization?> FindOrganization(
        IdentityProvider identityProvider,
        string externalTenantId,
        CancellationToken cancellationToken = default)
    {
        ReceivedIdentityProvider = identityProvider;
        ReceivedExternalTenantId = externalTenantId;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(Result);
    }
}

internal sealed class StubOrganizationMemberQueries : IOrganizationMemberQueries
{
    public OrganizationMember? FoundMember { get; set; }

    public OrganizationMember? CreatedMember { get; private set; }

    public Guid? ReceivedOrganizationId { get; private set; }

    public IdentityProvider? ReceivedIdentityProvider { get; private set; }

    public string? ReceivedExternalUserId { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public int FindMemberCallCount { get; private set; }

    public Task<OrganizationMember?> FindMember(
        Guid organizationId,
        IdentityProvider identityProvider,
        string externalUserId,
        CancellationToken cancellationToken = default)
    {
        FindMemberCallCount++;
        ReceivedOrganizationId = organizationId;
        ReceivedIdentityProvider = identityProvider;
        ReceivedExternalUserId = externalUserId;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(FoundMember);
    }

    public Task<OrganizationMember> CreateMember(
        OrganizationMember member,
        CancellationToken cancellationToken = default)
    {
        CreatedMember = member;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(member);
    }

    public Task<IReadOnlyCollection<OrganizationMember>> GetMembers(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<OrganizationMember?> FindMember(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<OrganizationMember> UpdateRole(
        OrganizationMember member,
        OrganizationRole role,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class StubAuthenticateUserService : IAuthenticateUserService
{
    public required (Organization Organization, OrganizationMember Member) Result { get; init; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<(Organization Organization, OrganizationMember Member)> GetOrganizationAsync(
        CancellationToken cancellationToken)
    {
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(Result);
    }
}
