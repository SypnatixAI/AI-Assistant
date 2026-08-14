namespace AssistantCore.ExternalServices.Entities.OpenAI.Models;

public sealed record OpenAiToolResult(
    string ToolCallId,
    string ResultJson);
