using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Repositories;

public enum UsagePolicyCreateStatus
{
    Created,
    VersionConflict,
    EffectiveAtConflict
}

public sealed record UsagePolicyCreateResult(
    UsagePolicyCreateStatus Status,
    OrganizationUsagePolicy? Policy)
{
    public static UsagePolicyCreateResult VersionConflict { get; } =
        new(UsagePolicyCreateStatus.VersionConflict, null);

    public static UsagePolicyCreateResult EffectiveAtConflict { get; } =
        new(UsagePolicyCreateStatus.EffectiveAtConflict, null);

    public static UsagePolicyCreateResult Created(OrganizationUsagePolicy policy) =>
        new(UsagePolicyCreateStatus.Created, policy);
}
