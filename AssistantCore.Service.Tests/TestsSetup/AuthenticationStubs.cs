using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Authentication;
using AssistantCore.Service.Application.Services.AuthenticateUser;

namespace AssistantCore.Service.Tests;

internal sealed class StubCurrentIdentity : ICurrentIdentity
{
    public AuthenticatedIdentity Identity { get; init; } = new(
        IdentityProvider.MicrosoftEntraId,
        "tenant-id",
        "user-id",
        "Test User",
        "test.user@example.com");

    public AuthenticatedIdentity GetIdentity() => Identity;
}

internal sealed class StubOrganizationQueries : IOrganizationQueries
{
    public Organization? Result { get; set; }

    public IdentityProvider? ReceivedIdentityProvider { get; private set; }

    public string? ReceivedExternalTenantId { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<Organization?> FindOrganization(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(Result?.Id == organizationId ? Result : null);
    }

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
    public IReadOnlyCollection<OrganizationMember> Members { get; init; } = [];

    public OrganizationMember? FoundMember { get; set; }

    public OrganizationMember? CreatedMember { get; private set; }

    public OrganizationMember? UpdatedMember { get; private set; }

    public Guid? ReceivedOrganizationId { get; private set; }

    public Guid? ReceivedMemberId { get; private set; }

    public IdentityProvider? ReceivedIdentityProvider { get; private set; }

    public string? ReceivedExternalUserId { get; private set; }

    public OrganizationRole? ReceivedRole { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public int FindMemberCallCount { get; private set; }

    public int GetMembersCallCount { get; private set; }

    public int FindMemberByIdCallCount { get; private set; }

    public int UpdateRoleCallCount { get; private set; }

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
        CancellationToken cancellationToken = default)
    {
        GetMembersCallCount++;
        ReceivedOrganizationId = organizationId;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(Members);
    }

    public Task<OrganizationMember?> FindMember(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        FindMemberByIdCallCount++;
        ReceivedOrganizationId = organizationId;
        ReceivedMemberId = memberId;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(FoundMember);
    }

    public Task<OrganizationMember> UpdateRole(
        OrganizationMember member,
        OrganizationRole role,
        CancellationToken cancellationToken = default)
    {
        UpdateRoleCallCount++;
        UpdatedMember = member;
        ReceivedRole = role;
        ReceivedCancellationToken = cancellationToken;
        member.Role = role;
        return Task.FromResult(member);
    }
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
