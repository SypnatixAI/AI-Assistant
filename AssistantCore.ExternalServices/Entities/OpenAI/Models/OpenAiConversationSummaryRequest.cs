namespace AssistantCore.ExternalServices.Entities.OpenAI.Models;

public sealed record OpenAiConversationSummaryRequest(
    string Model,
    string Instructions,
    string CurrentUserMessage,
    string CurrentAssistantMessage,
    IReadOnlyCollection<OpenAiConversationMessage> ConversationHistory);
