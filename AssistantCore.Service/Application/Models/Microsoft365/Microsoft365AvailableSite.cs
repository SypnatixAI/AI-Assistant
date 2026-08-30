namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365AvailableSite(
    string SiteId,
    string DisplayName,
    string WebUrl);
