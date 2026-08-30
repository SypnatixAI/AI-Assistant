namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365AvailableSiteResponse(
    string SiteId,
    string DisplayName,
    string WebUrl,
    bool IsSelected);
