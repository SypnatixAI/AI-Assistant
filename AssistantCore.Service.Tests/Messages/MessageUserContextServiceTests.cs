using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Authentication;
using AssistantCore.Service.Application.Services.Messages.Authorization;
using AssistantCore.Service.Application.Services.TenantAdmission;

namespace AssistantCore.Service.Tests.Messages;

public sealed class MessageUserContextServiceTests
{
    [Theory]
    [InlineAutoDomainData(OrganizationRole.Admin)]
    [InlineAutoDomainData(OrganizationRole.User)]
    public async Task Given_AnActiveAuthorizedMember_When_GetCurrentAsync_Then_ReturnsUserContext(
        OrganizationRole role,
        CancellationToken cancellationToken,
        Organization organization,
        OrganizationMember member,
        AuthenticatedIdentity identity)
    {
        // Given
        organization.Status = RecordStatus.Active;
        member.OrganizationId = organization.Id;
        member.Status = RecordStatus.Active;
        member.Role = role;
        var organizationQueries = new StubOrganizationQueries { Result = organization };
        var memberQueries = new StubOrganizationMemberQueries { FoundMember = member };
        var service = new MessageUserContextService(
            new StubCurrentIdentity { Identity = identity },
            organizationQueries,
            memberQueries,
            new StubOrganizationRoleResolver { Role = role },
            new StubMicrosoft365OnboardingCompletionChecker(),
            new TenantAdmissionPolicy());

        // When
        var result = await service.GetCurrentAsync(cancellationToken);

        // Then
        Assert.Same(organization, result.Organization);
        Assert.Same(member, result.Member);
        Assert.Equal(identity.Provider, organizationQueries.ReceivedIdentityProvider);
        Assert.Equal(identity.ExternalOrganizationId, organizationQueries.ReceivedExternalTenantId);
        Assert.Equal(organization.Id, memberQueries.ReceivedOrganizationId);
        Assert.Equal(identity.Provider, memberQueries.ReceivedIdentityProvider);
        Assert.Equal(identity.ExternalUserId, memberQueries.ReceivedExternalUserId);
        Assert.Equal(cancellationToken, memberQueries.ReceivedCancellationToken);
        Assert.Null(memberQueries.CreatedMember);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnknownOrganization_When_GetCurrentAsync_Then_ThrowsForbidden(
        AuthenticatedIdentity identity)
    {
        // Given
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new MessageUserContextService(
            new StubCurrentIdentity { Identity = identity },
            new StubOrganizationQueries(),
            memberQueries,
            new StubOrganizationRoleResolver(),
            new StubMicrosoft365OnboardingCompletionChecker(),
            new TenantAdmissionPolicy());

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetCurrentAsync(CancellationToken.None));

        // Then
        Assert.Equal("Organization access denied.", exception.Message);
        Assert.Equal(0, memberQueries.FindMemberCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInactiveOrganization_When_GetCurrentAsync_Then_ThrowsForbidden(
        Organization organization,
        AuthenticatedIdentity identity)
    {
        // Given
        organization.Status = RecordStatus.Inactive;
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new MessageUserContextService(
            new StubCurrentIdentity { Identity = identity },
            new StubOrganizationQueries { Result = organization },
            memberQueries,
            new StubOrganizationRoleResolver(),
            new StubMicrosoft365OnboardingCompletionChecker(),
            new TenantAdmissionPolicy());

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetCurrentAsync(CancellationToken.None));

        // Then
        Assert.Equal("Organization access denied.", exception.Message);
        Assert.Equal(0, memberQueries.FindMemberCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnknownMember_When_GetCurrentAsync_Then_ThrowsForbidden(
        Organization organization,
        AuthenticatedIdentity identity)
    {
        // Given
        organization.Status = RecordStatus.Active;
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new MessageUserContextService(
            new StubCurrentIdentity { Identity = identity },
            new StubOrganizationQueries { Result = organization },
            memberQueries,
            new StubOrganizationRoleResolver(),
            new StubMicrosoft365OnboardingCompletionChecker(),
            new TenantAdmissionPolicy());

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetCurrentAsync(CancellationToken.None));

        // Then
        Assert.Equal("Organization member access denied.", exception.Message);
        Assert.Null(memberQueries.CreatedMember);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInactiveMember_When_GetCurrentAsync_Then_ThrowsForbidden(
        Organization organization,
        OrganizationMember member,
        AuthenticatedIdentity identity)
    {
        // Given
        organization.Status = RecordStatus.Active;
        member.OrganizationId = organization.Id;
        member.Status = RecordStatus.Inactive;
        var memberQueries = new StubOrganizationMemberQueries { FoundMember = member };
        var service = new MessageUserContextService(
            new StubCurrentIdentity { Identity = identity },
            new StubOrganizationQueries { Result = organization },
            memberQueries,
            new StubOrganizationRoleResolver(),
            new StubMicrosoft365OnboardingCompletionChecker(),
            new TenantAdmissionPolicy());

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetCurrentAsync(CancellationToken.None));

        // Then
        Assert.Equal("Organization member access denied.", exception.Message);
        Assert.Null(memberQueries.CreatedMember);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInvalidIndicativeDatabaseRole_When_GetCurrentAsync_Then_UsesTokenRole(
        Organization organization,
        OrganizationMember member,
        AuthenticatedIdentity identity)
    {
        // Given
        organization.Status = RecordStatus.Active;
        member.OrganizationId = organization.Id;
        member.Status = RecordStatus.Active;
        member.Role = (OrganizationRole)999;
        var memberQueries = new StubOrganizationMemberQueries { FoundMember = member };
        var service = new MessageUserContextService(
            new StubCurrentIdentity { Identity = identity },
            new StubOrganizationQueries { Result = organization },
            memberQueries,
            new StubOrganizationRoleResolver { Role = OrganizationRole.User },
            new StubMicrosoft365OnboardingCompletionChecker { IsComplete = true },
            new TenantAdmissionPolicy());

        // When
        var result = await service.GetCurrentAsync(CancellationToken.None);

        // Then
        Assert.Equal(OrganizationRole.User, result.Member.Role);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInvalidIdentity_When_GetCurrentAsync_Then_PropagatesUnauthorized(
        UnauthorizedAccessException expectedException)
    {
        // Given
        var organizationQueries = new StubOrganizationQueries();
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new MessageUserContextService(
            new ThrowingCurrentIdentity(expectedException),
            organizationQueries,
            memberQueries,
            new StubOrganizationRoleResolver(),
            new StubMicrosoft365OnboardingCompletionChecker(),
            new TenantAdmissionPolicy());

        // When
        var exception = await Record.ExceptionAsync(() =>
            service.GetCurrentAsync(CancellationToken.None));

        // Then
        Assert.Same(expectedException, exception);
        Assert.Null(organizationQueries.ReceivedIdentityProvider);
        Assert.Equal(0, memberQueries.FindMemberCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnIncompleteSetupAndAStandardUser_When_GetCurrentAsync_Then_ThrowsTenantAdmissionException(
        Organization organization,
        OrganizationMember member,
        AuthenticatedIdentity identity)
    {
        // Given
        organization.Status = RecordStatus.Active;
        member.OrganizationId = organization.Id;
        member.Status = RecordStatus.Active;
        member.Role = OrganizationRole.Admin;
        var memberQueries = new StubOrganizationMemberQueries { FoundMember = member };
        var onboardingCompletionChecker = new StubMicrosoft365OnboardingCompletionChecker
        {
            IsComplete = false
        };
        var service = new MessageUserContextService(
            new StubCurrentIdentity { Identity = identity },
            new StubOrganizationQueries { Result = organization },
            memberQueries,
            new StubOrganizationRoleResolver { Role = OrganizationRole.User },
            onboardingCompletionChecker,
            new TenantAdmissionPolicy());

        // When
        var exception = await Assert.ThrowsAsync<TenantAdmissionException>(() =>
            service.GetCurrentAsync(CancellationToken.None));

        // Then
        Assert.Equal(TenantAdmissionException.TenantAdminRequired, exception.ErrorCode);
        Assert.Equal(organization.Id, onboardingCompletionChecker.ReceivedOrganizationId);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnIncompleteSetupAndATenantAdmin_When_GetCurrentAsync_Then_ReturnsUserContext(
        Organization organization,
        OrganizationMember member,
        AuthenticatedIdentity identity)
    {
        // Given
        organization.Status = RecordStatus.Active;
        member.OrganizationId = organization.Id;
        member.Status = RecordStatus.Active;
        member.Role = OrganizationRole.User;
        var memberQueries = new StubOrganizationMemberQueries { FoundMember = member };
        var service = new MessageUserContextService(
            new StubCurrentIdentity { Identity = identity },
            new StubOrganizationQueries { Result = organization },
            memberQueries,
            new StubOrganizationRoleResolver { Role = OrganizationRole.Admin },
            new StubMicrosoft365OnboardingCompletionChecker { IsComplete = false },
            new TenantAdmissionPolicy());

        // When
        var result = await service.GetCurrentAsync(CancellationToken.None);

        // Then
        Assert.Same(member, result.Member);
        Assert.Equal(OrganizationRole.Admin, result.Member.Role);
    }

    [Theory, AutoDomainData]
    public async Task Given_ACompleteSetupAndAStandardUser_When_GetCurrentAsync_Then_ReturnsUserContext(
        Organization organization,
        OrganizationMember member,
        AuthenticatedIdentity identity)
    {
        // Given
        organization.Status = RecordStatus.Active;
        member.OrganizationId = organization.Id;
        member.Status = RecordStatus.Active;
        member.Role = OrganizationRole.Admin;
        var memberQueries = new StubOrganizationMemberQueries { FoundMember = member };
        var service = new MessageUserContextService(
            new StubCurrentIdentity { Identity = identity },
            new StubOrganizationQueries { Result = organization },
            memberQueries,
            new StubOrganizationRoleResolver { Role = OrganizationRole.User },
            new StubMicrosoft365OnboardingCompletionChecker { IsComplete = true },
            new TenantAdmissionPolicy());

        // When
        var result = await service.GetCurrentAsync(CancellationToken.None);

        // Then
        Assert.Same(member, result.Member);
        Assert.Equal(OrganizationRole.User, result.Member.Role);
    }

    private sealed class ThrowingCurrentIdentity(Exception exception) : ICurrentIdentity
    {
        public AuthenticatedIdentity GetIdentity() => throw exception;
    }
}
