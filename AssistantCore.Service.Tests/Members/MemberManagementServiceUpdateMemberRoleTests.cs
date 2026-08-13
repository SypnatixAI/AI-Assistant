using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Members;

namespace AssistantCore.Service.Tests.Members;

public sealed class MemberManagementServiceUpdateMemberRoleTests
{
    [Theory, AutoDomainData]
    public async Task Given_AUser_When_UpdateMemberRoleAsync_Then_ThrowsForbiddenWithoutQueryingMember(
        Guid targetMemberId,
        Organization organization,
        OrganizationMember currentMember)
    {
        // Given
        var context = CreateContext(organization, currentMember, OrganizationRole.User);

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => context.Service.UpdateMemberRoleAsync(
                targetMemberId,
                "Admin",
                CancellationToken.None));

        // Then
        Assert.Equal("Administrator access required.", exception.Message);
        Assert.Equal(0, context.MemberQueries.FindMemberByIdCallCount);
        Assert.Equal(0, context.MemberQueries.UpdateRoleCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyMemberIdentifier_When_UpdateMemberRoleAsync_Then_ThrowsBadRequestWithoutQueryingMember(
        Organization organization,
        OrganizationMember currentMember)
    {
        // Given
        var context = CreateContext(organization, currentMember);

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => context.Service.UpdateMemberRoleAsync(
                Guid.Empty,
                "Admin",
                CancellationToken.None));

        // Then
        Assert.Equal("Member identifier is required.", exception.Message);
        Assert.Equal(0, context.MemberQueries.FindMemberByIdCallCount);
        Assert.Equal(0, context.MemberQueries.UpdateRoleCallCount);
    }

    [Theory]
    [InlineAutoDomainData("")]
    [InlineAutoDomainData("Manager")]
    [InlineAutoDomainData("admin")]
    [InlineAutoDomainData((object?)null)]
    public async Task Given_AnInvalidRole_When_UpdateMemberRoleAsync_Then_ThrowsBadRequestWithoutQueryingMember(
        string? role,
        Guid targetMemberId,
        Organization organization,
        OrganizationMember currentMember)
    {
        // Given
        var context = CreateContext(organization, currentMember);

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => context.Service.UpdateMemberRoleAsync(
                targetMemberId,
                role!,
                CancellationToken.None));

        // Then
        Assert.Equal("Role must be 'Admin' or 'User'.", exception.Message);
        Assert.Equal(0, context.MemberQueries.FindMemberByIdCallCount);
        Assert.Equal(0, context.MemberQueries.UpdateRoleCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheCurrentAdminAsTarget_When_UpdateMemberRoleAsync_Then_ThrowsBadRequestWithoutQueryingMember(
        Organization organization,
        OrganizationMember currentMember)
    {
        // Given
        var context = CreateContext(organization, currentMember);

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => context.Service.UpdateMemberRoleAsync(
                context.CurrentAdmin.Id,
                "User",
                CancellationToken.None));

        // Then
        Assert.Equal("An administrator cannot change their own role.", exception.Message);
        Assert.Equal(0, context.MemberQueries.FindMemberByIdCallCount);
        Assert.Equal(0, context.MemberQueries.UpdateRoleCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnknownMember_When_UpdateMemberRoleAsync_Then_ThrowsNotFoundWithOrganizationScope(
        CancellationToken cancellationToken,
        Guid memberId,
        Organization organization,
        OrganizationMember currentMember)
    {
        // Given
        var context = CreateContext(organization, currentMember);

        // When
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.UpdateMemberRoleAsync(
                memberId,
                "Admin",
                cancellationToken));

        // Then
        Assert.Equal("Organization member not found.", exception.Message);
        Assert.Equal(context.Organization.Id, context.MemberQueries.ReceivedOrganizationId);
        Assert.Equal(memberId, context.MemberQueries.ReceivedMemberId);
        Assert.Equal(cancellationToken, context.MemberQueries.ReceivedCancellationToken);
        Assert.Equal(1, context.MemberQueries.FindMemberByIdCallCount);
        Assert.Equal(0, context.MemberQueries.UpdateRoleCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInactiveMember_When_UpdateMemberRoleAsync_Then_ThrowsBadRequestWithoutUpdatingRole(
        Organization organization,
        OrganizationMember currentMember,
        OrganizationMember target)
    {
        // Given
        var context = CreateContext(organization, currentMember);
        target.OrganizationId = context.Organization.Id;
        target.Role = OrganizationRole.User;
        target.Status = RecordStatus.Inactive;
        context.MemberQueries.FoundMember = target;

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => context.Service.UpdateMemberRoleAsync(
                target.Id,
                "Admin",
                CancellationToken.None));

        // Then
        Assert.Equal("An inactive organization member role cannot be changed.", exception.Message);
        Assert.Equal(1, context.MemberQueries.FindMemberByIdCallCount);
        Assert.Equal(0, context.MemberQueries.UpdateRoleCallCount);
    }

    [Theory]
    [InlineAutoDomainData("Admin", OrganizationRole.Admin)]
    [InlineAutoDomainData("User", OrganizationRole.User)]
    public async Task Given_AValidRole_When_UpdateMemberRoleAsync_Then_UpdatesAndReturnsMember(
        string role,
        OrganizationRole expectedRole,
        CancellationToken cancellationToken,
        Organization organization,
        OrganizationMember currentMember,
        OrganizationMember target)
    {
        // Given
        var initialRole = expectedRole == OrganizationRole.Admin
            ? OrganizationRole.User
            : OrganizationRole.Admin;
        var context = CreateContext(organization, currentMember);
        target.OrganizationId = context.Organization.Id;
        target.Role = initialRole;
        target.Status = RecordStatus.Active;
        context.MemberQueries.FoundMember = target;

        // When
        var result = await context.Service.UpdateMemberRoleAsync(
            target.Id,
            role,
            cancellationToken);

        // Then
        Assert.Same(target, result);
        Assert.Same(target, context.MemberQueries.UpdatedMember);
        Assert.Equal(expectedRole, result.Role);
        Assert.Equal(expectedRole, context.MemberQueries.ReceivedRole);
        Assert.Equal(context.Organization.Id, context.MemberQueries.ReceivedOrganizationId);
        Assert.Equal(target.Id, context.MemberQueries.ReceivedMemberId);
        Assert.Equal(cancellationToken, context.MemberQueries.ReceivedCancellationToken);
        Assert.Equal(1, context.MemberQueries.FindMemberByIdCallCount);
        Assert.Equal(1, context.MemberQueries.UpdateRoleCallCount);
    }

    private static TestContext CreateContext(
        Organization organization,
        OrganizationMember currentMember,
        OrganizationRole currentRole = OrganizationRole.Admin,
        OrganizationMember? target = null)
    {
        currentMember.OrganizationId = organization.Id;
        currentMember.Role = currentRole;
        currentMember.Status = RecordStatus.Active;
        var memberQueries = new StubOrganizationMemberQueries { FoundMember = target };
        var service = new MemberManagementService(
            new StubAuthenticateUserService { Result = (organization, currentMember) },
            memberQueries);

        return new TestContext(service, memberQueries, organization, currentMember);
    }

    private sealed record TestContext(
        MemberManagementService Service,
        StubOrganizationMemberQueries MemberQueries,
        Organization Organization,
        OrganizationMember CurrentAdmin);
}
