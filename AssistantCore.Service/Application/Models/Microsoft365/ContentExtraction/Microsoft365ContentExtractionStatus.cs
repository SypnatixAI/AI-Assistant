namespace AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;

public enum Microsoft365ContentExtractionStatus
{
    Success,
    EmptyDocument,
    EncryptedDocument,
    CorruptedDocument,
    UnsupportedFormat,
    TooLarge
}
