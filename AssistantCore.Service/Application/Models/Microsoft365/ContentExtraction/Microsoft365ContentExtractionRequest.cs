namespace AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;

public sealed record Microsoft365ContentExtractionRequest(
    string FileName,
    string? MimeType,
    Stream Content,
    long? ContentLength = null);
