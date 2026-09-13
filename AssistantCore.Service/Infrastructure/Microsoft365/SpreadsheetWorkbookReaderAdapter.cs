using System.Xml;
using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Tabular;
using AssistantCore.Service.Application.Services.Messages.Tabular;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class SpreadsheetWorkbookReaderAdapter(
    MicrosoftExcelTableReaderClient client,
    IOptions<Microsoft365Options> options) : ISpreadsheetWorkbookReader
{
    public async Task<SpreadsheetWorkbook> ReadAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var limits = options.Value;
        MicrosoftExcelTableWorkbook workbook;
        try
        {
            workbook = await client.ReadAsync(
                fileName,
                content,
                limits.MaximumExtractionExpandedSizeBytes,
                limits.MaximumExcelSheets,
                limits.MaximumExcelCells,
                cancellationToken);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException(
                "The workbook contains invalid XML.",
                exception);
        }

        return new SpreadsheetWorkbook(workbook.Worksheets
            .Select(worksheet => new SpreadsheetWorksheet(
                worksheet.Name,
                worksheet.Columns,
                worksheet.Rows))
            .ToArray());
    }
}
