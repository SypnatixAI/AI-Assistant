using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365CsvContentExtractorAdapter(
    MicrosoftCsvContentExtractorClient client,
    IOptions<Microsoft365Options> options) : IMicrosoft365ContentExtractor
{
    public bool CanExtract(string fileName, string? mimeType) =>
        !string.IsNullOrWhiteSpace(fileName)
        && Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase);

    public async Task<Microsoft365ContentExtractionResult> ExtractAsync(
        Microsoft365ContentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await client.ExtractAsync(
            request.FileName,
            request.MimeType,
            request.Content,
            request.ContentLength,
            options.Value.MaximumExtractionFileSizeBytes,
            options.Value.MaximumCsvRows,
            options.Value.MaximumExtractedCharacters,
            cancellationToken);

        return Map(result);
    }

    internal static Microsoft365ContentExtractionResult Map(MicrosoftTextBasedExtractionResult result) =>
        new(
            MapStatus(result.Status),
            result.Units.Select(MapUnit).ToArray(),
            []);

    private static Microsoft365ContentExtractionStatus MapStatus(MicrosoftTextBasedExtractionStatus status) => status switch
    {
        MicrosoftTextBasedExtractionStatus.Success => Microsoft365ContentExtractionStatus.Success,
        MicrosoftTextBasedExtractionStatus.EmptyDocument => Microsoft365ContentExtractionStatus.EmptyDocument,
        MicrosoftTextBasedExtractionStatus.CorruptedDocument => Microsoft365ContentExtractionStatus.CorruptedDocument,
        MicrosoftTextBasedExtractionStatus.UnsupportedFormat => Microsoft365ContentExtractionStatus.UnsupportedFormat,
        MicrosoftTextBasedExtractionStatus.TooLarge => Microsoft365ContentExtractionStatus.TooLarge,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static Microsoft365ExtractedContentUnit MapUnit(MicrosoftTextBasedExtractedUnit unit) =>
        new(
            unit.IsTitle ? Microsoft365ExtractedContentUnitKind.Title : Microsoft365ExtractedContentUnitKind.Table,
            unit.Order,
            unit.Text,
            unit.SourcePart);
}
