using System.Text.Json;

namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftListColumn(
    string Id,
    JsonElement Definition);
