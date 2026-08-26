namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftWordExtractionResult(
    MicrosoftWordExtractionStatus Status,
    IReadOnlyList<MicrosoftWordExtractedUnit> Units,
    IReadOnlyList<MicrosoftWordExtractionWarning> Warnings);

public enum MicrosoftWordExtractionStatus
{
    Success,
    EmptyDocument,
    EncryptedDocument,
    CorruptedDocument,
    UnsupportedFormat,
    TooLarge
}

public enum MicrosoftWordExtractionWarning
{
    MacroIgnored,
    ExternalLinkIgnored,
    EmbeddedObjectIgnored
}
