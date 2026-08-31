using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public interface IMessageToolOrchestrator
{
    Task<MessageOrchestrationResult> OrchestrateAsync(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        IReadOnlyCollection<AiToolDefinition> availableTools,
        CancellationToken cancellationToken);

    Task<MessageOrchestrationResult> OrchestrateStreamingAsync(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        IReadOnlyCollection<AiToolDefinition> availableTools,
        Func<string, CancellationToken, ValueTask> onProgress,
        Func<string, CancellationToken, ValueTask> onAnswerDelta,
        CancellationToken cancellationToken);
}
