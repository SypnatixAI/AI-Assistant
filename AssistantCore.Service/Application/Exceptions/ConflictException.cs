namespace AssistantCore.Service.Application.Exceptions;

public sealed class ConflictException(string message, string? errorCode = null)
    : Exception(message), IErrorCodeException
{
    public const string ConversationArchived = "conversation_archived";

    public const string ConversationVersionConflict = "conversation_version_conflict";

    public const string MemberVersionConflict = "member_version_conflict";

    public const string UsagePolicyVersionConflict = "usage_policy_version_conflict";

    public const string UsagePolicyEffectiveAtConflict = "usage_policy_effective_at_conflict";

    public string ErrorCode { get; } = errorCode ?? "conflict";
}
