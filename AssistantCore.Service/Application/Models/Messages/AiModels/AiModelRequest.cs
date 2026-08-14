using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Models.Messages.AiModels;

public sealed record AiModelRequest(
    SelectedAiModel Model,
    string Instructions,
    string UserMessage,
    IReadOnlyCollection<AiToolDefinition> AvailableTools,
    IReadOnlyCollection<AiRequestedToolCall> PreviousToolCalls,
    IReadOnlyCollection<ToolExecutionResult> ToolResults);
