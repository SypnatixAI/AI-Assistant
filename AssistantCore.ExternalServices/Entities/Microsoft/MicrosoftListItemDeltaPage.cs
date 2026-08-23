namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftListItemDeltaPage(
    IReadOnlyCollection<MicrosoftListItemDelta> Items,
    string? DeltaLink);
