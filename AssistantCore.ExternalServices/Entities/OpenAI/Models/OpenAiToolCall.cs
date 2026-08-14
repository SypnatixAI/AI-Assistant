namespace AssistantCore.ExternalServices.Entities.OpenAI.Models;

public sealed record OpenAiToolCall(
    string CallId,
    string Name,
    string ArgumentsJson);
