using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Services.Members;

namespace AssistantCore.Service.Tests.Members;

public sealed class MemberManagementServiceGetMembersTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnAdmin_When_GetMembersAsync_Then_ReturnsOrganizationMembers(
        CancellationToken cancellationToken,
        Organization organization,
        OrganizationMember admin,
        OrganizationMember user)
    {
        // Given
        admin.OrganizationId = organization.Id;
        admin.Role = OrganizationRole.Admin;
        user.OrganizationId = organization.Id;
        user.Role = OrganizationRole.User;
        OrganizationMember[] members =
        [
            admin,
            user
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

    [Theory, AutoDomainData]
    public async Task Given_AUser_When_GetMembersAsync_Then_ThrowsForbiddenWithoutQueryingMembers(
        Organization organization,
        OrganizationMember user)
    {
        // Given
        user.OrganizationId = organization.Id;
        user.Role = OrganizationRole.User;
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

}
