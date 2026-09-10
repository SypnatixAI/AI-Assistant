namespace AssistantCore.ExternalServices.Entities.Microsoft;

public enum MicrosoftTextBasedExtractionStatus
{
    Success,
    EmptyDocument,
    CorruptedDocument,
    UnsupportedFormat,
    TooLarge
}

public sealed record MicrosoftTextBasedExtractedUnit(
    int Order,
    string Text,
    string SourcePart,
    bool IsTitle);

public sealed record MicrosoftTextBasedExtractionResult(
    MicrosoftTextBasedExtractionStatus Status,
    IReadOnlyList<MicrosoftTextBasedExtractedUnit> Units)
{
    public static MicrosoftTextBasedExtractionResult Empty(MicrosoftTextBasedExtractionStatus status) =>
        new(status, []);
}
