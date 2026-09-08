namespace AssistantCore.Service.Application.Exceptions;

public class BadRequestException(string message, string? errorCode = null)
    : Exception(message), IErrorCodeException
{
    public const string InvalidConversationStatus = "invalid_conversation_status";

    public const string InvalidPagination = "invalid_pagination";

    public const string InvalidVersionHeader = "invalid_version_header";

    public const string EmptyConversationPatch = "empty_conversation_patch";

    public const string InvalidConversationTitle = "invalid_conversation_title";

    public string ErrorCode { get; } = errorCode ?? "bad_request";
}
