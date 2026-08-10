namespace AssistantCore.Service.Application.Models.Messages.Lifecycle;

public sealed record StartedMessageProcessing(
    Guid ConversationId,
    Guid UserMessageId,
    string UserMessage);
