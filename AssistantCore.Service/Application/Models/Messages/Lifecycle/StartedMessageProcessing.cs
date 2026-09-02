using AssistantCore.Service.Application.Models.Conversations;
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

    /// <summary>
    /// Resume de la conversation lorsque cet envoi vient de la creer. Reste null
    /// lorsque le message rejoint une conversation existante : le client la
    /// connait alors deja et n'a rien a inserer dans sa liste.
    /// </summary>
    public ConversationSummaryResponse? CreatedConversation { get; init; }
}
