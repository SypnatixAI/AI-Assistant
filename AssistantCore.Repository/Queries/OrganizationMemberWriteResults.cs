using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Queries;

public enum MemberUpdateStatus
{
    Updated,
    NotFound,
    VersionConflict
}

public sealed record MemberUpdateResult(
    MemberUpdateStatus Status,
    OrganizationMember? Member)
{
    public static MemberUpdateResult NotFound { get; } =
        new(MemberUpdateStatus.NotFound, null);

    public static MemberUpdateResult VersionConflict { get; } =
        new(MemberUpdateStatus.VersionConflict, null);

    public static MemberUpdateResult Updated(OrganizationMember member) =>
        new(MemberUpdateStatus.Updated, member);
}
