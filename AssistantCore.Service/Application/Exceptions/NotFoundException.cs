namespace AssistantCore.Service.Application.Exceptions;

public sealed class NotFoundException(string message, string? errorCode = null)
    : Exception(message), IErrorCodeException
{
    public const string ConversationNotFound = "conversation_not_found";

    public string ErrorCode { get; } = errorCode ?? "not_found";
}
