using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Authentication;
using AssistantCore.Service.Application.Services.Usage;

namespace AssistantCore.Service.Tests.Usage;

public sealed class UsagePolicyManagementServiceTests
{
    private static readonly Guid ActorId = Guid.NewGuid();

    [Fact]
    public async Task Given_ANegativeLimit_When_SetUsagePolicyAsync_Then_ThrowsBadRequestWithoutQueryingAnything()
    {
        // Given
        var context = CreateContext();

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => context.Service.SetUsagePolicyAsync(
                context.Organization.Id,
                -1,
                "Active",
                "2026-09-01T00:00:00Z",
                CancellationToken.None));

        // Then
        Assert.Equal("monthlyTokenLimit must be zero or a positive integer.", exception.Message);
        Assert.Equal(0, context.PolicyRepository.CreateCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Suspended2")]
    [InlineData("active")]
    public async Task Given_AnInvalidStatus_When_SetUsagePolicyAsync_Then_ThrowsBadRequest(string status)
    {
        // Given
        var context = CreateContext();

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => context.Service.SetUsagePolicyAsync(
                context.Organization.Id,
                1_000_000,
                status,
                "2026-09-01T00:00:00Z",
                CancellationToken.None));

        // Then
        Assert.Equal("status must be 'Active' or 'Suspended'.", exception.Message);
        Assert.Equal(0, context.PolicyRepository.CreateCallCount);
    }

    [Theory]
    [InlineData("2026-09-01T00:00:00")]
    [InlineData("not-a-date")]
    [InlineData("")]
    public async Task Given_AnEffectiveDateWithoutAnExplicitOffset_When_SetUsagePolicyAsync_Then_ThrowsBadRequest(
        string effectiveAt)
    {
        // Given
        var context = CreateContext();

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => context.Service.SetUsagePolicyAsync(
                context.Organization.Id,
                1_000_000,
                "Active",
                effectiveAt,
                CancellationToken.None));

        // Then
        Assert.Equal("effectiveAt must include an explicit UTC offset.", exception.Message);
        Assert.Equal(0, context.PolicyRepository.CreateCallCount);
    }

    [Fact]
    public async Task Given_AnUnknownOrganization_When_SetUsagePolicyAsync_Then_ThrowsNotFoundWithoutCreatingAPolicy()
    {
        // Given
        var context = CreateContext();

        // When
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.SetUsagePolicyAsync(
                Guid.NewGuid(),
                1_000_000,
                "Active",
                "2026-09-01T00:00:00Z",
                CancellationToken.None));

        // Then
        Assert.Equal("Organization not found.", exception.Message);
        Assert.Equal(0, context.PolicyRepository.CreateCallCount);
    }

    [Fact]
    public async Task Given_AValidRequest_When_SetUsagePolicyAsync_Then_CreatesThePolicyWithTheResolvedActorAndCorrelationId()
    {
        // Given
        var context = CreateContext();
        var policy = new OrganizationUsagePolicy
        {
            Id = Guid.NewGuid(),
            OrganizationId = context.Organization.Id,
            Version = 1,
            MonthlyTokenLimit = 1_000_000,
            Status = UsagePolicyStatus.Active,
            EffectiveAt = DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            CreatedAt = DateTimeOffset.Parse("2026-08-18T15:00:00Z"),
            ActorId = ActorId
        };
        context.PolicyRepository.CreateResult = UsagePolicyCreateResult.Created(policy);

        // When
        var result = await context.Service.SetUsagePolicyAsync(
            context.Organization.Id,
            1_000_000,
            "Active",
            "2026-09-01T00:00:00Z",
            CancellationToken.None);

        // Then
        Assert.Same(policy, result);
        Assert.Equal(context.Organization.Id, context.PolicyRepository.ReceivedOrganizationId);
        Assert.Equal(1_000_000, context.PolicyRepository.ReceivedMonthlyTokenLimit);
        Assert.Equal(UsagePolicyStatus.Active, context.PolicyRepository.ReceivedStatus);
        Assert.Equal(
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            context.PolicyRepository.ReceivedEffectiveAt);
        Assert.Equal(ActorId, context.PolicyRepository.ReceivedActorId);
        Assert.NotNull(context.PolicyRepository.ReceivedCorrelationId);
    }

    [Fact]
    public async Task Given_AVersionConflict_When_SetUsagePolicyAsync_Then_ThrowsConflict()
    {
        // Given
        var context = CreateContext();
        context.PolicyRepository.CreateResult = UsagePolicyCreateResult.VersionConflict;

        // When
        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.Service.SetUsagePolicyAsync(
                context.Organization.Id,
                1_000_000,
                "Active",
                "2026-09-01T00:00:00Z",
                CancellationToken.None));

        // Then
        Assert.Equal(ConflictException.UsagePolicyVersionConflict, exception.ErrorCode);
    }

    [Fact]
    public async Task Given_AnEffectiveAtConflict_When_SetUsagePolicyAsync_Then_ThrowsConflict()
    {
        // Given
        var context = CreateContext();
        context.PolicyRepository.CreateResult = UsagePolicyCreateResult.EffectiveAtConflict;

        // When
        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.Service.SetUsagePolicyAsync(
                context.Organization.Id,
                1_000_000,
                "Active",
                "2026-09-01T00:00:00Z",
                CancellationToken.None));

        // Then
        Assert.Equal(ConflictException.UsagePolicyEffectiveAtConflict, exception.ErrorCode);
    }

    private static TestContext CreateContext()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Contoso",
            Domain = "contoso.example.com",
            IdentityProvider = IdentityProvider.MicrosoftEntraId,
            Status = RecordStatus.Active
        };
        var organizationQueries = new StubOrganizationQueries { Result = organization };
        var policyRepository = new StubOrganizationUsagePolicyRepository();
        var currentIdentity = new StubCurrentIdentity
        {
            Identity = new AuthenticatedIdentity(
                IdentityProvider.MicrosoftEntraId,
                "tenant-id",
                ActorId.ToString(),
                null,
                null,
                ["UsagePolicy.Manage"])
        };
        var service = new UsagePolicyManagementService(
            organizationQueries,
            policyRepository,
            currentIdentity,
            new StubCorrelationIdProvider(),
            new StubTimeProvider { UtcNow = DateTimeOffset.Parse("2026-08-18T15:00:00Z") });

        return new TestContext(service, organization, policyRepository);
    }

    private sealed record TestContext(
        UsagePolicyManagementService Service,
        Organization Organization,
        StubOrganizationUsagePolicyRepository PolicyRepository);
}
