using System.Text.Json;

namespace AssistantCore.Service.Application.Models.Messages.Tools;

public sealed record AiRequestedToolCall(
    string CallId,
    string ToolName,
    JsonElement Arguments);
