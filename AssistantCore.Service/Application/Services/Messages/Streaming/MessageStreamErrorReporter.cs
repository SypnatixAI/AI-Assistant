using AssistantCore.Service.Application.Exceptions;
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
        var providerStatusCode = exception is AiProviderUnavailableException unavailable
            ? unavailable.ProviderStatusCode
            : null;
        var providerErrorMessage = exception is AiProviderUnavailableException providerUnavailable
            ? providerUnavailable.ProviderErrorMessage
            : null;

        logger.LogError(
            exception,
            "Message generation failed for conversation {ConversationId}, user message {UserMessageId}. Error code: {ErrorCode}; provider status: {ProviderStatusCode}; provider error: {ProviderErrorMessage}",
            conversationId,
            userMessageId,
            errorCode,
            providerStatusCode,
            providerErrorMessage);
    }
}
