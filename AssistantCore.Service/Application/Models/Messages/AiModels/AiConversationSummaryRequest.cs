namespace AssistantCore.Service.Application.Models.Messages.AiModels;

public sealed record AiConversationSummaryRequest(
    SelectedAiModel Model,
    string Instructions,
    IReadOnlyCollection<AiConversationMessage> ConversationHistory,
    string CurrentUserMessage,
    string CurrentAssistantMessage);
