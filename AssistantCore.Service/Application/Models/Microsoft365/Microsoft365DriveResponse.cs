namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365DriveResponse(
    string? SiteId,
    string DriveId,
    string DisplayName,
    string? WebUrl,
    string Status,
    bool IsIndexed,
    string SourceType = "sharepoint",
    string? OwnerUserObjectId = null,
    string? OwnerUserPrincipalName = null);
