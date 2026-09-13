namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftPdfExtractionResult(
    MicrosoftPdfExtractionStatus Status,
    IReadOnlyList<MicrosoftPdfPage> Pages);

public sealed record MicrosoftPdfPage(int PageNumber, string Text);

public enum MicrosoftPdfExtractionStatus
{
    Success,
    EmptyDocument,
    EncryptedDocument,
    CorruptedDocument,
    TooLarge
}
