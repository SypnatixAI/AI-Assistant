namespace AssistantCore.Service.Application.Services.Messages.Streaming;

public interface IMessageStreamErrorReporter
{
    void Report(
        Exception exception,
        Guid? conversationId,
        Guid? userMessageId,
        string errorCode);
}
