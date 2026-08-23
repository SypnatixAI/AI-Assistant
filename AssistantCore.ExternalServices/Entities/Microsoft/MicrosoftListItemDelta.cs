using System.Text.Json;

namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftListItemDelta(
    string Id,
    string? ETag,
    DateTimeOffset? CreatedDateTime,
    DateTimeOffset? LastModifiedDateTime,
    string? WebUrl,
    JsonElement? Fields,
    bool IsDeleted);
