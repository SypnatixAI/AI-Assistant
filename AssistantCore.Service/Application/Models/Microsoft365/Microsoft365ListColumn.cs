using System.Text.Json;

namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365ListColumn(
    string Id,
    JsonElement Definition);
