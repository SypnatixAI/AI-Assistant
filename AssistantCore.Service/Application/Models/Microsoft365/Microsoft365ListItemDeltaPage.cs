namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365ListItemDeltaPage(
    IReadOnlyCollection<Microsoft365ListItemDelta> Items,
    string? DeltaLink);
