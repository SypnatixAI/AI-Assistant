using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365PdfImageContentExtractorAdapter(
    MicrosoftPdfContentExtractorClient pdfClient,
    MicrosoftVisionReadClient ocrClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365ContentExtractor
{
    private static readonly HashSet<string> PdfExtensions = [".pdf"];
    private static readonly HashSet<string> ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"];
    private static readonly HashSet<string> SupportedImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/bmp", "image/tiff"
    };

    public bool CanExtract(string fileName, string? mimeType) =>
        (PdfExtensions.Contains(Path.GetExtension(fileName))
            && (string.IsNullOrWhiteSpace(mimeType) || IsMimeType(mimeType, "application/pdf")))
        || (ImageExtensions.Contains(Path.GetExtension(fileName))
            && (string.IsNullOrWhiteSpace(mimeType) || IsImageMimeType(mimeType)));

    public async Task<Microsoft365ContentExtractionResult> ExtractAsync(
        Microsoft365ContentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var configuration = options.Value;
        var extension = Path.GetExtension(request.FileName);
        MicrosoftPdfExtractionResult? native = null;
        if (PdfExtensions.Contains(extension))
        {
            native = await pdfClient.ExtractAsync(
                request.Content,
                request.ContentLength,
                configuration.MaximumExtractionFileSizeBytes,
                configuration.MaximumPdfPages,
                configuration.MaximumExtractedCharacters,
                cancellationToken);
            if (native.Status == MicrosoftPdfExtractionStatus.Success
                && native.Pages.All(page => page.Text.Length >= configuration.OcrMinimumNativeCharactersPerPage))
            {
                return MapNative(native);
            }

            if (string.IsNullOrWhiteSpace(configuration.OcrEndpoint)
                || string.IsNullOrWhiteSpace(configuration.OcrApiKey))
            {
                return MapNative(native);
            }
        }

        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }
        var ocr = await ocrClient.ReadAsync(
            configuration.OcrEndpoint,
            configuration.OcrApiKey,
            request.Content,
            GetMediaType(request, extension),
            configuration.OcrTimeoutSeconds,
            configuration.OcrPollIntervalMilliseconds,
            cancellationToken);
        return native is not null
            ? MergePdfResults(native, ocr, configuration.OcrMinimumNativeCharactersPerPage)
            : MapOcr(ocr);
    }

    private static string GetMediaType(Microsoft365ContentExtractionRequest request, string extension) =>
        request.MimeType?.Split(';', 2)[0].Trim()
        ?? extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            _ => "image/jpeg"
        };

    private static bool IsMimeType(string? mimeType, string expected) =>
        string.Equals(mimeType?.Split(';', 2)[0].Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsImageMimeType(string? mimeType) =>
        mimeType is not null
        && SupportedImageMimeTypes.Contains(mimeType.Split(';', 2)[0].Trim());

    private static Microsoft365ContentExtractionResult MapNative(MicrosoftPdfExtractionResult result) =>
        new(
            result.Status switch
            {
                MicrosoftPdfExtractionStatus.Success => Microsoft365ContentExtractionStatus.Success,
                MicrosoftPdfExtractionStatus.EmptyDocument => Microsoft365ContentExtractionStatus.NoIndexableContent,
                MicrosoftPdfExtractionStatus.EncryptedDocument => Microsoft365ContentExtractionStatus.EncryptedDocument,
                MicrosoftPdfExtractionStatus.CorruptedDocument => Microsoft365ContentExtractionStatus.CorruptedDocument,
                MicrosoftPdfExtractionStatus.TooLarge => Microsoft365ContentExtractionStatus.TooLarge,
                _ => throw new ArgumentOutOfRangeException(nameof(result.Status), result.Status, null)
            },
            result.Pages.Select(page => new Microsoft365ExtractedContentUnit(
                Microsoft365ExtractedContentUnitKind.Page,
                page.PageNumber,
                page.Text,
                $"page:{page.PageNumber}")).ToArray(),
            []);

    private static Microsoft365ContentExtractionResult MapOcr(MicrosoftOcrResult result) =>
        new(
            result.Status switch
            {
                MicrosoftOcrStatus.Success => Microsoft365ContentExtractionStatus.Success,
                MicrosoftOcrStatus.NoIndexableContent => Microsoft365ContentExtractionStatus.NoIndexableContent,
                MicrosoftOcrStatus.TooLarge => Microsoft365ContentExtractionStatus.TooLarge,
                MicrosoftOcrStatus.Timeout => Microsoft365ContentExtractionStatus.OcrTimeout,
                MicrosoftOcrStatus.Unavailable => Microsoft365ContentExtractionStatus.OcrUnavailable,
                _ => throw new ArgumentOutOfRangeException(nameof(result.Status), result.Status, null)
            },
            result.Pages.Select(page => new Microsoft365ExtractedContentUnit(
                Microsoft365ExtractedContentUnitKind.Page,
                page.PageNumber,
                page.Text,
                $"ocr-page:{page.PageNumber}")).ToArray(),
            []);

    private static Microsoft365ContentExtractionResult MergePdfResults(
        MicrosoftPdfExtractionResult native,
        MicrosoftOcrResult ocr,
        int minimumNativeCharacters)
    {
        if (ocr.Status != MicrosoftOcrStatus.Success)
        {
            return MapNative(native);
        }

        var ocrByPage = ocr.Pages.ToDictionary(page => page.PageNumber);
        var nativeByPage = native.Pages.ToDictionary(page => page.PageNumber);
        var pages = ocr.Pages
            .Select(page => nativeByPage.TryGetValue(page.PageNumber, out var nativePage)
                && nativePage.Text.Length >= minimumNativeCharacters
                    ? nativePage
                    : new MicrosoftPdfPage(page.PageNumber, page.Text))
            .Concat(native.Pages.Where(page => !ocrByPage.ContainsKey(page.PageNumber)))
            .OrderBy(page => page.PageNumber)
            .ToArray();

        return new Microsoft365ContentExtractionResult(
            pages.Length == 0
                ? Microsoft365ContentExtractionStatus.NoIndexableContent
                : Microsoft365ContentExtractionStatus.Success,
            pages.Select(page => new Microsoft365ExtractedContentUnit(
                Microsoft365ExtractedContentUnitKind.Page,
                page.PageNumber,
                page.Text,
                nativeByPage.TryGetValue(page.PageNumber, out var nativePage)
                    && nativePage.Text == page.Text
                    ? $"page:{page.PageNumber}"
                    : $"ocr-page:{page.PageNumber}")).ToArray(),
            []);
    }
}
