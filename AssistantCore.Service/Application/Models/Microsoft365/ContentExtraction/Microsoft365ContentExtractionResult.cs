namespace AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;

public sealed record Microsoft365ContentExtractionResult(
    Microsoft365ContentExtractionStatus Status,
    IReadOnlyList<Microsoft365ExtractedContentUnit> Units,
    IReadOnlyList<Microsoft365ContentExtractionWarning> Warnings)
{
    public string Text => string.Join(
        Environment.NewLine,
        Units.OrderBy(unit => unit.Order).Select(unit => unit.Text));

    public static Microsoft365ContentExtractionResult Unsupported() =>
        new(
            Microsoft365ContentExtractionStatus.UnsupportedFormat,
            [],
            []);
}
