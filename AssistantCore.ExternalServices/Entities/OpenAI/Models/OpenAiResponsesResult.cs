namespace AssistantCore.ExternalServices.Entities.OpenAI.Models;

public sealed record OpenAiResponsesResult(
    string OutputText,
    IReadOnlyCollection<OpenAiToolCall> ToolCalls,
    int InputTokens,
    int OutputTokens);
