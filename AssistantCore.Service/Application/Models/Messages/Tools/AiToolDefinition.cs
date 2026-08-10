using System.Text.Json;

namespace AssistantCore.Service.Application.Models.Messages.Tools;

public sealed record AiToolDefinition(
    string Name,
    string Description,
    JsonElement InputSchema);
