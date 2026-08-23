using System.Text.Json;

namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365ListItemDelta(
    string Id,
    string? ETag,
    DateTimeOffset? CreatedDateTime,
    DateTimeOffset? LastModifiedDateTime,
    string? WebUrl,
    JsonElement? Fields,
    bool IsDeleted);
