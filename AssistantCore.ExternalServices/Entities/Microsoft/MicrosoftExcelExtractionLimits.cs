namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftExcelExtractionLimits(
    long MaximumFileSizeBytes,
    long MaximumExpandedSizeBytes,
    int MaximumExtractedCharacters,
    int MaximumSheets,
    int MaximumCells)
{
    public void Validate()
    {
        if (MaximumFileSizeBytes <= 0
            || MaximumExpandedSizeBytes <= 0
            || MaximumExtractedCharacters <= 0
            || MaximumSheets <= 0
            || MaximumCells <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumFileSizeBytes),
                "Extraction limits must be greater than zero.");
        }
    }
}
