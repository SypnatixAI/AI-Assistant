namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365SiteResponse(
    string SiteId,
    string DisplayName,
    string? WebUrl,
    string Status);
