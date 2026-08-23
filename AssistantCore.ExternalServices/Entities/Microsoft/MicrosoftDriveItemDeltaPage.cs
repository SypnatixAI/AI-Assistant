namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftDriveItemDeltaPage(
    IReadOnlyCollection<MicrosoftDriveItemDelta> Items,
    string? DeltaLink);
