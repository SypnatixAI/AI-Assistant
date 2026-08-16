namespace AssistantCore.Service.Application.Exceptions;

public sealed class ToolCallValidationException(
    string toolCallId,
    string message) : Exception(message)
{
    public const string TechnicalCode = "TOOL_CALL_REJECTED";

    public string ToolCallId { get; } = toolCallId;
}
