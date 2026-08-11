using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Models.Authentication;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class AuthenticateUserServiceTests
{
    [Fact]
    public async Task Given_AnExistingActiveMember_When_GettingOrganization_Then_ReturnsOrganizationAndMember()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var organization = CreateOrganization();
        var member = CreateMember(organization.Id);
        var identity = new StubCurrentIdentity();
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

    [Fact]
    public async Task Given_AnUnknownOrganization_When_GettingOrganization_Then_ThrowsForbiddenException()
    {
        // Given
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new AuthenticateUserService(
            new StubCurrentIdentity(),
            memberQueries,
            new StubOrganizationQueries());

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => service.GetOrganizationAsync(CancellationToken.None));

        // Then
        Assert.Equal("Organization access denied.", exception.Message);
        Assert.Equal(0, memberQueries.FindMemberCallCount);
    }

    [Fact]
    public async Task Given_NoExistingMember_When_GettingOrganization_Then_CreatesAnActiveUser()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var organization = CreateOrganization();
        var identity = new StubCurrentIdentity
        {
            Identity = CreateIdentity(
                externalOrganizationId: "customer-tenant",
                externalUserId: "external-user",
                displayName: "Marie Tremblay",
                email: "marie@example.com")
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
        Assert.Equal("Marie Tremblay", createdMember.Name);
        Assert.Equal("marie@example.com", createdMember.Email);
        Assert.Equal(IdentityProvider.MicrosoftEntraId, createdMember.IdentityProvider);
        Assert.Equal("external-user", createdMember.ExternalUserId);
        Assert.Equal(OrganizationRole.User, createdMember.Role);
        Assert.Equal(RecordStatus.Active, createdMember.Status);
        Assert.Equal(cancellationToken, memberQueries.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Given_NoDisplayName_When_CreatingMember_Then_UsesEmailAsDisplayName()
    {
        // Given
        var organization = CreateOrganization();
        var identity = new StubCurrentIdentity
        {
            Identity = CreateIdentity(
                displayName: null,
                email: "fallback@example.com")
        };
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new AuthenticateUserService(
            identity,
            memberQueries,
            new StubOrganizationQueries { Result = organization });

        // When
        await service.GetOrganizationAsync(CancellationToken.None);

        // Then
        Assert.Equal("fallback@example.com", memberQueries.CreatedMember?.Name);
    }

    [Fact]
    public async Task Given_NoEmail_When_CreatingMember_Then_ThrowsUnauthorizedException()
    {
        // Given
        var organization = CreateOrganization();
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new AuthenticateUserService(
            new StubCurrentIdentity
            {
                Identity = CreateIdentity(email: null)
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

    [Fact]
    public async Task Given_AnInactiveMember_When_GettingOrganization_Then_ThrowsForbiddenException()
    {
        // Given
        var organization = CreateOrganization();
        var member = CreateMember(organization.Id);
        member.Status = RecordStatus.Inactive;
        var service = new AuthenticateUserService(
            new StubCurrentIdentity(),
            new StubOrganizationMemberQueries { FoundMember = member },
            new StubOrganizationQueries { Result = organization });

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => service.GetOrganizationAsync(CancellationToken.None));

        // Then
        Assert.Equal("Organization member access denied.", exception.Message);
    }

    private static Organization CreateOrganization() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Contoso",
        IdentityProvider = IdentityProvider.MicrosoftEntraId,
        ExternalTenantId = "tenant-id",
        Status = RecordStatus.Active
    };

    private static OrganizationMember CreateMember(Guid organizationId) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        Name = "Test User",
        Email = "test.user@example.com",
        IdentityProvider = IdentityProvider.MicrosoftEntraId,
        ExternalUserId = "user-id",
        Role = OrganizationRole.User,
        Status = RecordStatus.Active
    };

    private static AuthenticatedIdentity CreateIdentity(
        string externalOrganizationId = "tenant-id",
        string externalUserId = "user-id",
        string? displayName = "Test User",
        string? email = "test.user@example.com") => new(
            IdentityProvider.MicrosoftEntraId,
            externalOrganizationId,
            externalUserId,
            displayName,
            email);
}
