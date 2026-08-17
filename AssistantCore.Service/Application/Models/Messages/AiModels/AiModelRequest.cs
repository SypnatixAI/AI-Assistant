using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Models.Messages.AiModels;

public sealed record AiModelRequest(
    SelectedAiModel Model,
    string Instructions,
    string UserMessage,
    IReadOnlyCollection<AiConversationMessage> ConversationHistory,
    IReadOnlyCollection<AiToolDefinition> AllowedTools,
    IReadOnlyCollection<AiRequestedToolCall> RequestedToolCalls,
    IReadOnlyCollection<ToolExecutionResult> ToolResults,
    AiModelContinuationContext? ContinuationContext = null);
