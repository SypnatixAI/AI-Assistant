using AssistantCore.Service.Application.Models.Messages.Tabular;

namespace AssistantCore.Service.Application.Services.Messages.Tabular;

public interface ISpreadsheetWorkbookReader
{
    Task<SpreadsheetWorkbook> ReadAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken);
}
