using System.Globalization;
using System.Text.RegularExpressions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Exceptions;

namespace AssistantCore.Service.Application.Services.Usage;

public sealed partial class UsagePolicyManagementService(
    IOrganizationQueries organizationQueries,
    IOrganizationUsagePolicyRepository usagePolicyRepository,
    ICurrentIdentity currentIdentity,
    ICorrelationIdProvider correlationIdProvider,
    TimeProvider timeProvider) : IUsagePolicyManagementService
{
    public async Task<OrganizationUsagePolicy> SetUsagePolicyAsync(
        Guid organizationId,
        long monthlyTokenLimit,
        string status,
        string effectiveAt,
        CancellationToken cancellationToken = default)
    {
        if (monthlyTokenLimit < 0)
        {
            throw new BadRequestException("monthlyTokenLimit must be zero or a positive integer.");
        }

        var parsedStatus = ParseStatus(status);
        var parsedEffectiveAt = ParseEffectiveAt(effectiveAt);
        var actorId = Guid.Parse(currentIdentity.GetIdentity().ExternalUserId);

        _ = await organizationQueries.FindOrganization(organizationId, cancellationToken)
            ?? throw new NotFoundException("Organization not found.");

        var createdAt = timeProvider.GetUtcNow();

        var result = await usagePolicyRepository.CreateAsync(
            organizationId,
            monthlyTokenLimit,
            parsedStatus,
            parsedEffectiveAt,
            actorId,
            createdAt,
            correlationIdProvider.GetCorrelationId(),
            cancellationToken);

        return result.Status switch
        {
            UsagePolicyCreateStatus.Created when result.Policy is not null => result.Policy,
            UsagePolicyCreateStatus.VersionConflict => throw new ConflictException(
                "A concurrent usage policy change was applied to this organization.",
                ConflictException.UsagePolicyVersionConflict),
            UsagePolicyCreateStatus.EffectiveAtConflict => throw new ConflictException(
                "Another policy already takes effect at this date and time.",
                ConflictException.UsagePolicyEffectiveAtConflict),
            _ => throw new InvalidOperationException("Unexpected usage policy creation result.")
        };
    }

    private static UsagePolicyStatus ParseStatus(string status) =>
        status switch
        {
            nameof(UsagePolicyStatus.Active) => UsagePolicyStatus.Active,
            nameof(UsagePolicyStatus.Suspended) => UsagePolicyStatus.Suspended,
            _ => throw new BadRequestException("status must be 'Active' or 'Suspended'.")
        };

    private static DateTimeOffset ParseEffectiveAt(string effectiveAt)
    {
        if (string.IsNullOrWhiteSpace(effectiveAt) || !ExplicitOffsetPattern().IsMatch(effectiveAt.Trim()))
        {
            throw new BadRequestException("effectiveAt must include an explicit UTC offset.");
        }

        if (!DateTimeOffset.TryParse(
                effectiveAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            throw new BadRequestException("effectiveAt must be a valid ISO 8601 date and time.");
        }

        return parsed;
    }

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex ExplicitOffsetPattern();
}
