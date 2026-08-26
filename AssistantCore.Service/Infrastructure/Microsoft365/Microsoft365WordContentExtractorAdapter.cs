using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365WordContentExtractorAdapter(
    MicrosoftWordContentExtractorClient client,
    IOptions<Microsoft365Options> options) : IMicrosoft365ContentExtractor
{
    private static readonly HashSet<string> WordExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".docm"
    };

    public bool CanExtract(string fileName, string? mimeType) =>
        !string.IsNullOrWhiteSpace(fileName)
        && WordExtensions.Contains(Path.GetExtension(fileName));

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
            options.Value.MaximumExtractionExpandedSizeBytes,
            options.Value.MaximumExtractedCharacters,
            cancellationToken);

        return new Microsoft365ContentExtractionResult(
            MapStatus(result.Status),
            result.Units.Select(MapUnit).ToArray(),
            result.Warnings.Select(MapWarning).ToArray());
    }

    private static Microsoft365ContentExtractionStatus MapStatus(
        MicrosoftWordExtractionStatus status) => status switch
    {
        MicrosoftWordExtractionStatus.Success => Microsoft365ContentExtractionStatus.Success,
        MicrosoftWordExtractionStatus.EmptyDocument => Microsoft365ContentExtractionStatus.EmptyDocument,
        MicrosoftWordExtractionStatus.EncryptedDocument => Microsoft365ContentExtractionStatus.EncryptedDocument,
        MicrosoftWordExtractionStatus.CorruptedDocument => Microsoft365ContentExtractionStatus.CorruptedDocument,
        MicrosoftWordExtractionStatus.UnsupportedFormat => Microsoft365ContentExtractionStatus.UnsupportedFormat,
        MicrosoftWordExtractionStatus.TooLarge => Microsoft365ContentExtractionStatus.TooLarge,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static Microsoft365ExtractedContentUnit MapUnit(MicrosoftWordExtractedUnit unit) =>
        new(
            unit.Kind switch
            {
                MicrosoftWordExtractedUnitKind.Header => Microsoft365ExtractedContentUnitKind.Header,
                MicrosoftWordExtractedUnitKind.Title => Microsoft365ExtractedContentUnitKind.Title,
                MicrosoftWordExtractedUnitKind.Paragraph => Microsoft365ExtractedContentUnitKind.Paragraph,
                MicrosoftWordExtractedUnitKind.ListItem => Microsoft365ExtractedContentUnitKind.ListItem,
                MicrosoftWordExtractedUnitKind.Table => Microsoft365ExtractedContentUnitKind.Table,
                MicrosoftWordExtractedUnitKind.Footer => Microsoft365ExtractedContentUnitKind.Footer,
                _ => throw new ArgumentOutOfRangeException(nameof(unit), unit.Kind, null)
            },
            unit.Order,
            unit.Text,
            unit.SourcePart);

    private static Microsoft365ContentExtractionWarning MapWarning(
        MicrosoftWordExtractionWarning warning) => warning switch
    {
        MicrosoftWordExtractionWarning.MacroIgnored => Microsoft365ContentExtractionWarning.MacroIgnored,
        MicrosoftWordExtractionWarning.ExternalLinkIgnored => Microsoft365ContentExtractionWarning.ExternalLinkIgnored,
        MicrosoftWordExtractionWarning.EmbeddedObjectIgnored => Microsoft365ContentExtractionWarning.EmbeddedObjectIgnored,
        _ => throw new ArgumentOutOfRangeException(nameof(warning), warning, null)
    };
}
