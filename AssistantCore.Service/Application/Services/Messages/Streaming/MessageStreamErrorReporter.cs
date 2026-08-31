using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Application.Services.Messages.Streaming;

public sealed class MessageStreamErrorReporter(
    ILogger<MessageStreamErrorReporter> logger) : IMessageStreamErrorReporter
{
    public void Report(
        Exception exception,
        Guid? conversationId,
        Guid? userMessageId,
        string errorCode)
    {
        logger.LogError(
            exception,
            "Message generation failed for conversation {ConversationId}, user message {UserMessageId}. Error code: {ErrorCode}",
            conversationId,
            userMessageId,
            errorCode);
    }
}
