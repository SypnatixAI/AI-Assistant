using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365ExcelContentExtractorAdapter(
    MicrosoftExcelContentExtractorClient client,
    IOptions<Microsoft365Options> options) : IMicrosoft365ContentExtractor
{
    private static readonly HashSet<string> ExcelExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xlsm"
    };

    public bool CanExtract(string fileName, string? mimeType) =>
        !string.IsNullOrWhiteSpace(fileName) && ExcelExtensions.Contains(Path.GetExtension(fileName));

    public async Task<Microsoft365ContentExtractionResult> ExtractAsync(
        Microsoft365ContentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await client.ExtractAsync(
            request.FileName, request.MimeType, request.Content, request.ContentLength,
            new MicrosoftExcelExtractionLimits(
                options.Value.MaximumExtractionFileSizeBytes,
                options.Value.MaximumExtractionExpandedSizeBytes,
                options.Value.MaximumExtractedCharacters,
                options.Value.MaximumExcelSheets,
                options.Value.MaximumExcelCells),
            cancellationToken);

        return new Microsoft365ContentExtractionResult(
            result.Status switch
            {
                MicrosoftExcelExtractionStatus.Success => Microsoft365ContentExtractionStatus.Success,
                MicrosoftExcelExtractionStatus.EmptyWorkbook => Microsoft365ContentExtractionStatus.EmptyDocument,
                MicrosoftExcelExtractionStatus.EncryptedWorkbook => Microsoft365ContentExtractionStatus.EncryptedDocument,
                MicrosoftExcelExtractionStatus.CorruptedWorkbook => Microsoft365ContentExtractionStatus.CorruptedDocument,
                MicrosoftExcelExtractionStatus.UnsupportedFormat => Microsoft365ContentExtractionStatus.UnsupportedFormat,
                MicrosoftExcelExtractionStatus.TooLarge => Microsoft365ContentExtractionStatus.TooLarge,
                _ => throw new ArgumentOutOfRangeException(nameof(result.Status), result.Status, null)
            },
            result.Units.Select(unit => new Microsoft365ExtractedContentUnit(
                unit.SourcePart.Contains('!')
                    ? Microsoft365ExtractedContentUnitKind.Table
                    : Microsoft365ExtractedContentUnitKind.Header,
                unit.Order,
                unit.Text,
                unit.SourcePart)).ToArray(),
            result.Warnings.Select(warning => warning switch
            {
                MicrosoftExcelExtractionWarning.MacroIgnored => Microsoft365ContentExtractionWarning.MacroIgnored,
                MicrosoftExcelExtractionWarning.ExternalLinkIgnored => Microsoft365ContentExtractionWarning.ExternalLinkIgnored,
                MicrosoftExcelExtractionWarning.HiddenSheetIgnored => Microsoft365ContentExtractionWarning.HiddenSheetIgnored,
                MicrosoftExcelExtractionWarning.FormulaValueUnavailable => Microsoft365ContentExtractionWarning.FormulaValueUnavailable,
                _ => throw new ArgumentOutOfRangeException(nameof(warning), warning, null)
            }).ToArray());
    }
}
