using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Authentication;
using AssistantCore.Service.Application.Services.Messages.Authorization;

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
            memberQueries);

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
            memberQueries);

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
            memberQueries);

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
            memberQueries);

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetCurrentAsync(CancellationToken.None));

        // Then
        Assert.Equal("Organization member access denied.", exception.Message);
        Assert.Null(memberQueries.CreatedMember);
    }

    [Theory]
    [InlineAutoDomainData(RecordStatus.Inactive, OrganizationRole.User)]
    [InlineAutoDomainData(RecordStatus.Active, (OrganizationRole)999)]
    public async Task Given_AnUnauthorizedMember_When_GetCurrentAsync_Then_ThrowsForbidden(
        RecordStatus status,
        OrganizationRole role,
        Organization organization,
        OrganizationMember member,
        AuthenticatedIdentity identity)
    {
        // Given
        organization.Status = RecordStatus.Active;
        member.OrganizationId = organization.Id;
        member.Status = status;
        member.Role = role;
        var memberQueries = new StubOrganizationMemberQueries { FoundMember = member };
        var service = new MessageUserContextService(
            new StubCurrentIdentity { Identity = identity },
            new StubOrganizationQueries { Result = organization },
            memberQueries);

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetCurrentAsync(CancellationToken.None));

        // Then
        Assert.Equal("Organization member access denied.", exception.Message);
        Assert.Null(memberQueries.CreatedMember);
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
            memberQueries);

        // When
        var exception = await Record.ExceptionAsync(() =>
            service.GetCurrentAsync(CancellationToken.None));

        // Then
        Assert.Same(expectedException, exception);
        Assert.Null(organizationQueries.ReceivedIdentityProvider);
        Assert.Equal(0, memberQueries.FindMemberCallCount);
    }

    private sealed class ThrowingCurrentIdentity(Exception exception) : ICurrentIdentity
    {
        public AuthenticatedIdentity GetIdentity() => throw exception;
    }
}
