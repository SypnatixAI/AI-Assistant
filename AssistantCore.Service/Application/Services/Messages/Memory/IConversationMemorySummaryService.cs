using AssistantCore.Service.Application.Models.Messages.AiModels;

namespace AssistantCore.Service.Application.Services.Messages.Memory;

public interface IConversationMemorySummaryService
{
    Task<string?> CreateAsync(
        SelectedAiModel model,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        string currentUserMessage,
        string currentAssistantMessage,
        CancellationToken cancellationToken);
}
