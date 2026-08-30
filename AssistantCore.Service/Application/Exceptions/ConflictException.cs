namespace AssistantCore.Service.Application.Exceptions;

public sealed class ConflictException(string message, string? errorCode = null)
    : Exception(message), IErrorCodeException
{
    public const string ConversationArchived = "conversation_archived";

    public const string ConversationVersionConflict = "conversation_version_conflict";

    public string ErrorCode { get; } = errorCode ?? "conflict";
}
