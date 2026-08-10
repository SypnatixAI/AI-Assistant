using System.Text.Json;

namespace AssistantCore.Service.Application.Models.Messages.Tools;

public sealed record ValidatedToolCall(
    string CallId,
    string ToolName,
    JsonElement Arguments);
