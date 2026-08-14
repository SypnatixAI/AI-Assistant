namespace AssistantCore.Service.Application.Models.Messages.Lifecycle;

public sealed record StartedMessageProcessing(
    Guid OrganizationId,
    Guid OwnerMemberId,
    Guid ConversationId,
    Guid UserMessageId,
    string UserMessage);
