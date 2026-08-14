namespace AssistantCore.ExternalServices.Entities.OpenAI.Models;

public sealed record OpenAiPreviousToolCall(
    string CallId,
    string ToolName,
    string ArgumentsJson);
