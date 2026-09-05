namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftExcelExtractionResult(
    MicrosoftExcelExtractionStatus Status,
    IReadOnlyList<MicrosoftExcelExtractedUnit> Units,
    IReadOnlyList<MicrosoftExcelExtractionWarning> Warnings);

public sealed record MicrosoftExcelExtractedUnit(
    int Order,
    string Text,
    string SourcePart);

public enum MicrosoftExcelExtractionStatus
{
    Success,
    EmptyWorkbook,
    EncryptedWorkbook,
    CorruptedWorkbook,
    UnsupportedFormat,
    TooLarge
}

public enum MicrosoftExcelExtractionWarning
{
    MacroIgnored,
    ExternalLinkIgnored,
    HiddenSheetIgnored,
    FormulaValueUnavailable
}
