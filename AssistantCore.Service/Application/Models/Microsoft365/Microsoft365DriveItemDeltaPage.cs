namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365DriveItemDeltaPage(
    IReadOnlyCollection<Microsoft365DriveItemDelta> Items,
    string? DeltaLink);
