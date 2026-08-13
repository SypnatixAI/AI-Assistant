using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Models.Authentication;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class AuthenticateUserServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnExistingActiveMember_When_GetOrganizationAsync_Then_ReturnsOrganizationAndMember(
        CancellationToken cancellationToken,
        Organization organization,
        OrganizationMember member,
        AuthenticatedIdentity authenticatedIdentity)
    {
        // Given
        member.OrganizationId = organization.Id;
        member.Status = RecordStatus.Active;
        var identity = new StubCurrentIdentity { Identity = authenticatedIdentity };
        var organizationQueries = new StubOrganizationQueries { Result = organization };
        var memberQueries = new StubOrganizationMemberQueries { FoundMember = member };
        var service = new AuthenticateUserService(identity, memberQueries, organizationQueries);

        // When
        var result = await service.GetOrganizationAsync(cancellationToken);

        // Then
        Assert.Same(organization, result.Organization);
        Assert.Same(member, result.Member);
        Assert.Equal(identity.Identity.Provider, organizationQueries.ReceivedIdentityProvider);
        Assert.Equal(identity.Identity.ExternalOrganizationId, organizationQueries.ReceivedExternalTenantId);
        Assert.Equal(organization.Id, memberQueries.ReceivedOrganizationId);
        Assert.Equal(identity.Identity.ExternalUserId, memberQueries.ReceivedExternalUserId);
        Assert.Equal(cancellationToken, organizationQueries.ReceivedCancellationToken);
        Assert.Equal(cancellationToken, memberQueries.ReceivedCancellationToken);
        Assert.Null(memberQueries.CreatedMember);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnknownOrganization_When_GetOrganizationAsync_Then_ThrowsForbiddenException(
        AuthenticatedIdentity authenticatedIdentity)
    {
        // Given
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new AuthenticateUserService(
            new StubCurrentIdentity { Identity = authenticatedIdentity },
            memberQueries,
            new StubOrganizationQueries());

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => service.GetOrganizationAsync(CancellationToken.None));

        // Then
        Assert.Equal("Organization access denied.", exception.Message);
        Assert.Equal(0, memberQueries.FindMemberCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoExistingMember_When_GetOrganizationAsync_Then_CreatesAnActiveUser(
        CancellationToken cancellationToken,
        Organization organization,
        AuthenticatedIdentity authenticatedIdentity)
    {
        // Given
        var identity = new StubCurrentIdentity
        {
            Identity = authenticatedIdentity
        };
        var organizationQueries = new StubOrganizationQueries { Result = organization };
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new AuthenticateUserService(identity, memberQueries, organizationQueries);

        // When
        var result = await service.GetOrganizationAsync(cancellationToken);

        // Then
        var createdMember = Assert.IsType<OrganizationMember>(memberQueries.CreatedMember);
        Assert.Same(createdMember, result.Member);
        Assert.NotEqual(Guid.Empty, createdMember.Id);
        Assert.Equal(organization.Id, createdMember.OrganizationId);
        Assert.Equal(authenticatedIdentity.DisplayName, createdMember.Name);
        Assert.Equal(authenticatedIdentity.Email, createdMember.Email);
        Assert.Equal(authenticatedIdentity.Provider, createdMember.IdentityProvider);
        Assert.Equal(authenticatedIdentity.ExternalUserId, createdMember.ExternalUserId);
        Assert.Equal(OrganizationRole.User, createdMember.Role);
        Assert.Equal(RecordStatus.Active, createdMember.Status);
        Assert.Equal(cancellationToken, memberQueries.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoDisplayName_When_GetOrganizationAsync_Then_UsesEmailAsDisplayName(
        Organization organization,
        AuthenticatedIdentity authenticatedIdentity)
    {
        // Given
        var identityWithoutDisplayName = authenticatedIdentity with { DisplayName = null };
        var identity = new StubCurrentIdentity
        {
            Identity = identityWithoutDisplayName
        };
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new AuthenticateUserService(
            identity,
            memberQueries,
            new StubOrganizationQueries { Result = organization });

        // When
        await service.GetOrganizationAsync(CancellationToken.None);

        // Then
        Assert.Equal(identityWithoutDisplayName.Email, memberQueries.CreatedMember?.Name);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoEmail_When_GetOrganizationAsync_Then_ThrowsUnauthorizedException(
        Organization organization,
        AuthenticatedIdentity authenticatedIdentity)
    {
        // Given
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new AuthenticateUserService(
            new StubCurrentIdentity
            {
                Identity = authenticatedIdentity with { Email = null }
            },
            memberQueries,
            new StubOrganizationQueries { Result = organization });

        // When
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetOrganizationAsync(CancellationToken.None));

        // Then
        Assert.Equal("Authenticated user email is missing.", exception.Message);
        Assert.Null(memberQueries.CreatedMember);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInactiveMember_When_GetOrganizationAsync_Then_ThrowsForbiddenException(
        Organization organization,
        OrganizationMember member,
        AuthenticatedIdentity authenticatedIdentity)
    {
        // Given
        member.OrganizationId = organization.Id;
        member.Status = RecordStatus.Inactive;
        var service = new AuthenticateUserService(
            new StubCurrentIdentity { Identity = authenticatedIdentity },
            new StubOrganizationMemberQueries { FoundMember = member },
            new StubOrganizationQueries { Result = organization });

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => service.GetOrganizationAsync(CancellationToken.None));

        // Then
        Assert.Equal("Organization member access denied.", exception.Message);
    }

}
