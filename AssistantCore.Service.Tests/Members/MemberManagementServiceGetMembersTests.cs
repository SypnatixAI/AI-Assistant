using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Services.Members;

namespace AssistantCore.Service.Tests.Members;

public sealed class MemberManagementServiceGetMembersTests
{
    [Fact]
    public async Task Given_AnAdmin_When_GetMembersAsync_Then_ReturnsOrganizationMembers()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var organization = CreateOrganization();
        var admin = CreateMember(organization.Id, OrganizationRole.Admin);
        OrganizationMember[] members =
        [
            admin,
            CreateMember(organization.Id, OrganizationRole.User)
        ];
        var authenticateUserService = new StubAuthenticateUserService
        {
            Result = (organization, admin)
        };
        var memberQueries = new StubOrganizationMemberQueries { Members = members };
        var service = new MemberManagementService(authenticateUserService, memberQueries);

        // When
        var result = await service.GetMembersAsync(cancellationToken);

        // Then
        Assert.Same(members, result);
        Assert.Equal(organization.Id, memberQueries.ReceivedOrganizationId);
        Assert.Equal(cancellationToken, authenticateUserService.ReceivedCancellationToken);
        Assert.Equal(cancellationToken, memberQueries.ReceivedCancellationToken);
        Assert.Equal(1, memberQueries.GetMembersCallCount);
    }

    [Fact]
    public async Task Given_AUser_When_GetMembersAsync_Then_ThrowsForbiddenWithoutQueryingMembers()
    {
        // Given
        var organization = CreateOrganization();
        var user = CreateMember(organization.Id, OrganizationRole.User);
        var memberQueries = new StubOrganizationMemberQueries();
        var service = new MemberManagementService(
            new StubAuthenticateUserService { Result = (organization, user) },
            memberQueries);

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => service.GetMembersAsync(CancellationToken.None));

        // Then
        Assert.Equal("Administrator access required.", exception.Message);
        Assert.Equal(0, memberQueries.GetMembersCallCount);
    }

    private static Organization CreateOrganization() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Contoso",
        IdentityProvider = IdentityProvider.MicrosoftEntraId,
        ExternalTenantId = "tenant-id",
        Status = RecordStatus.Active
    };

    private static OrganizationMember CreateMember(
        Guid organizationId,
        OrganizationRole role) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        Name = $"{role} Member",
        Email = $"{role.ToString().ToLowerInvariant()}.{Guid.NewGuid():N}@example.com",
        IdentityProvider = IdentityProvider.MicrosoftEntraId,
        ExternalUserId = Guid.NewGuid().ToString(),
        Role = role,
        Status = RecordStatus.Active
    };
}
