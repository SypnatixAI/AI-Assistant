using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Services.Usage;

namespace AssistantCore.Service.Tests;

internal sealed class StubOrganizationUsagePolicyRepository : IOrganizationUsagePolicyRepository
{
    public OrganizationUsagePolicy? EffectivePolicy { get; init; }

    public UsagePolicyCreateResult CreateResult { get; set; } =
        UsagePolicyCreateResult.VersionConflict;

    public Guid? ReceivedOrganizationId { get; private set; }

    public long? ReceivedMonthlyTokenLimit { get; private set; }

    public UsagePolicyStatus? ReceivedStatus { get; private set; }

    public DateTimeOffset? ReceivedEffectiveAt { get; private set; }

    public Guid? ReceivedActorId { get; private set; }

    public DateTimeOffset? ReceivedCreatedAt { get; private set; }

    public string? ReceivedCorrelationId { get; private set; }

    public DateTimeOffset? ReceivedAsOf { get; private set; }

    public int CreateCallCount { get; private set; }

    public Task<UsagePolicyCreateResult> CreateAsync(
        Guid organizationId,
        long monthlyTokenLimit,
        UsagePolicyStatus status,
        DateTimeOffset effectiveAt,
        Guid actorId,
        DateTimeOffset createdAt,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        CreateCallCount++;
        ReceivedOrganizationId = organizationId;
        ReceivedMonthlyTokenLimit = monthlyTokenLimit;
        ReceivedStatus = status;
        ReceivedEffectiveAt = effectiveAt;
        ReceivedActorId = actorId;
        ReceivedCreatedAt = createdAt;
        ReceivedCorrelationId = correlationId;
        return Task.FromResult(CreateResult);
    }

    public Task<OrganizationUsagePolicy?> FindEffectiveAsync(
        Guid organizationId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        ReceivedOrganizationId = organizationId;
        ReceivedAsOf = asOf;
        return Task.FromResult(EffectivePolicy);
    }
}

internal sealed class StubUsagePolicyManagementService : IUsagePolicyManagementService
{
    public OrganizationUsagePolicy? Policy { get; set; }

    public Guid? ReceivedOrganizationId { get; private set; }

    public long? ReceivedMonthlyTokenLimit { get; private set; }

    public string? ReceivedStatus { get; private set; }

    public string? ReceivedEffectiveAt { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<OrganizationUsagePolicy> SetUsagePolicyAsync(
        Guid organizationId,
        long monthlyTokenLimit,
        string status,
        string effectiveAt,
        CancellationToken cancellationToken = default)
    {
        ReceivedOrganizationId = organizationId;
        ReceivedMonthlyTokenLimit = monthlyTokenLimit;
        ReceivedStatus = status;
        ReceivedEffectiveAt = effectiveAt;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(
            Policy ?? throw new InvalidOperationException("Policy is not configured."));
    }
}
