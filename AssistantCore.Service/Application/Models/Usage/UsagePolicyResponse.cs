using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Models.Usage;

public sealed record UsagePolicyResponse(
    Guid OrganizationId,
    int Version,
    long MonthlyTokenLimit,
    string Status,
    DateTimeOffset EffectiveAt,
    DateTimeOffset CreatedAt)
{
    public static UsagePolicyResponse FromPolicy(OrganizationUsagePolicy policy) =>
        new(
            policy.OrganizationId,
            policy.Version,
            policy.MonthlyTokenLimit,
            policy.Status.ToString(),
            policy.EffectiveAt,
            policy.CreatedAt);
}
