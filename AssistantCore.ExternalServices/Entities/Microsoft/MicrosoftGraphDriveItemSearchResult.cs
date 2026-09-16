namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftGraphDriveItemSearchResult(
    string DriveId,
    string DriveItemId,
    string Name,
    string? WebUrl);
