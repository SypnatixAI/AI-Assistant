using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Authentication;
using AssistantCore.Service.Application.Services.Members;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Members;

public sealed class MemberManagementServiceUpdateMemberStatusTests
{
    private const string TenantAdminRole = "tenantAdmin";

    [Theory, AutoDomainData]
    public async Task Given_NoTenantAdminClaim_When_UpdateMemberStatusAsync_Then_ThrowsForbiddenWithoutQueryingMember(
        Guid targetMemberId,
        Organization organization,
        OrganizationMember currentMember)
    {
        // Given
        var context = CreateContext(organization, currentMember, hasTenantAdminClaim: false);

        // When
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => context.Service.UpdateMemberStatusAsync(
                targetMemberId,
                "Inactive",
                null,
                CancellationToken.None));

        // Then
        Assert.Equal("Administrator access required.", exception.Message);
        Assert.Equal(0, context.MemberQueries.UpdateStatusCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyMemberIdentifier_When_UpdateMemberStatusAsync_Then_ThrowsBadRequestWithoutQueryingMember(
        Organization organization,
        OrganizationMember currentMember)
    {
        // Given
        var context = CreateContext(organization, currentMember);

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => context.Service.UpdateMemberStatusAsync(
                Guid.Empty,
                "Inactive",
                null,
                CancellationToken.None));

        // Then
        Assert.Equal("Member identifier is required.", exception.Message);
        Assert.Equal(0, context.MemberQueries.UpdateStatusCallCount);
    }

    [Theory]
    [InlineAutoDomainData("")]
    [InlineAutoDomainData("Enabled")]
    [InlineAutoDomainData("active")]
    [InlineAutoDomainData((object?)null)]
    public async Task Given_AnInvalidStatus_When_UpdateMemberStatusAsync_Then_ThrowsBadRequestWithoutQueryingMember(
        string? status,
        Guid targetMemberId,
        Organization organization,
        OrganizationMember currentMember)
    {
        // Given
        var context = CreateContext(organization, currentMember);

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => context.Service.UpdateMemberStatusAsync(
                targetMemberId,
                status!,
                null,
                CancellationToken.None));

        // Then
        Assert.Equal("Status must be 'Active' or 'Inactive'.", exception.Message);
        Assert.Equal(0, context.MemberQueries.UpdateStatusCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheCurrentAdminAsTarget_When_UpdateMemberStatusAsync_Then_ThrowsBadRequestWithoutQueryingMember(
        Organization organization,
        OrganizationMember currentMember)
    {
        // Given
        var context = CreateContext(organization, currentMember);

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => context.Service.UpdateMemberStatusAsync(
                context.CurrentMember.Id,
                "Inactive",
                null,
                CancellationToken.None));

        // Then
        Assert.Equal("An administrator cannot change their own status.", exception.Message);
        Assert.Equal(0, context.MemberQueries.UpdateStatusCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnknownMember_When_UpdateMemberStatusAsync_Then_ThrowsNotFound(
        CancellationToken cancellationToken,
        Guid targetMemberId,
        Organization organization,
        OrganizationMember currentMember)
    {
        // Given
        var context = CreateContext(organization, currentMember);
        context.MemberQueries.UpdateStatusResult = MemberUpdateResult.NotFound;

        // When
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.UpdateMemberStatusAsync(
                targetMemberId,
                "Inactive",
                null,
                cancellationToken));

        // Then
        Assert.Equal("Organization member not found.", exception.Message);
        Assert.Equal(context.Organization.Id, context.MemberQueries.ReceivedOrganizationId);
        Assert.Equal(targetMemberId, context.MemberQueries.ReceivedMemberId);
        Assert.Equal(RecordStatus.Inactive, context.MemberQueries.ReceivedStatus);
        Assert.Equal(cancellationToken, context.MemberQueries.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_AConcurrentModification_When_UpdateMemberStatusAsync_Then_ThrowsConflict(
        Guid targetMemberId,
        Organization organization,
        OrganizationMember currentMember)
    {
        // Given
        var context = CreateContext(organization, currentMember);
        context.MemberQueries.UpdateStatusResult = MemberUpdateResult.VersionConflict;

        // When
        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.Service.UpdateMemberStatusAsync(
                targetMemberId,
                "Inactive",
                5,
                CancellationToken.None));

        // Then
        Assert.Equal(ConflictException.MemberVersionConflict, exception.ErrorCode);
    }

    [Theory]
    [InlineAutoDomainData("Active", RecordStatus.Active)]
    [InlineAutoDomainData("Inactive", RecordStatus.Inactive)]
    public async Task Given_AValidStatus_When_UpdateMemberStatusAsync_Then_UpdatesAndReturnsMember(
        string status,
        RecordStatus expectedStatus,
        CancellationToken cancellationToken,
        Guid targetMemberId,
        Organization organization,
        OrganizationMember currentMember,
        OrganizationMember target)
    {
        // Given
        var context = CreateContext(organization, currentMember);
        context.MemberQueries.UpdateStatusResult = MemberUpdateResult.Updated(target);

        // When
        var result = await context.Service.UpdateMemberStatusAsync(
            targetMemberId,
            status,
            3,
            cancellationToken);

        // Then
        Assert.Same(target, result);
        Assert.Equal(1, context.MemberQueries.UpdateStatusCallCount);
        Assert.Equal(context.Organization.Id, context.MemberQueries.ReceivedOrganizationId);
        Assert.Equal(targetMemberId, context.MemberQueries.ReceivedMemberId);
        Assert.Equal(expectedStatus, context.MemberQueries.ReceivedStatus);
        Assert.Equal(3, context.MemberQueries.ReceivedExpectedVersion);
        Assert.Equal(currentMember.Id, context.MemberQueries.ReceivedActorId);
        Assert.NotNull(context.MemberQueries.ReceivedCorrelationId);
        Assert.Equal(cancellationToken, context.MemberQueries.ReceivedCancellationToken);
    }

    private static TestContext CreateContext(
        Organization organization,
        OrganizationMember currentMember,
        bool hasTenantAdminClaim = true)
    {
        currentMember.OrganizationId = organization.Id;
        currentMember.Status = RecordStatus.Active;
        var memberQueries = new StubOrganizationMemberQueries();
        var identity = new StubCurrentIdentity
        {
            Identity = new AuthenticatedIdentity(
                IdentityProvider.MicrosoftEntraId,
                "tenant-id",
                "user-id",
                currentMember.Name,
                currentMember.Email,
                hasTenantAdminClaim ? [TenantAdminRole] : [])
        };
        var service = new MemberManagementService(
            new StubAuthenticateUserService { Result = (organization, currentMember) },
            memberQueries,
            identity,
            Options.Create(new OrganizationRoleOptions { TenantAdminRole = TenantAdminRole }),
            new StubCorrelationIdProvider(),
            new StubTimeProvider());

        return new TestContext(service, memberQueries, organization, currentMember);
    }

    private sealed record TestContext(
        MemberManagementService Service,
        StubOrganizationMemberQueries MemberQueries,
        Organization Organization,
        OrganizationMember CurrentMember);
}
