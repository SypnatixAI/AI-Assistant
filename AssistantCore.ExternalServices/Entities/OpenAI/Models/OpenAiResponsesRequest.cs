namespace AssistantCore.ExternalServices.Entities.OpenAI.Models;

public sealed record OpenAiResponsesRequest(
    string Model,
    string Instructions,
    string UserMessage,
    IReadOnlyCollection<OpenAiConversationMessage> ConversationHistory,
    IReadOnlyCollection<OpenAiToolDefinition> AvailableTools,
    IReadOnlyCollection<OpenAiPreviousToolCall> PreviousToolCalls,
    IReadOnlyCollection<OpenAiToolResult> ToolResults,
    string? PreviousResponseId);
