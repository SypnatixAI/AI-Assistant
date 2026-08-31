using AssistantCore.Service.Application.Models.Messages.AiModels;

namespace AssistantCore.Service.Application.Models.Messages.Lifecycle;

public sealed record StartedMessageProcessing(
    Guid OrganizationId,
    Guid OwnerMemberId,
    Guid ConversationId,
    Guid UserMessageId,
    string UserMessage)
{
    public IReadOnlyCollection<AiConversationMessage> ConversationHistory { get; init; } = [];

    public SelectedAiModel? SelectedModel { get; set; }
}
