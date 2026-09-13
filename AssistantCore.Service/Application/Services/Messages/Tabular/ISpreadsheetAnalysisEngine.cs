using AssistantCore.Service.Application.Models.Messages.Tabular;

namespace AssistantCore.Service.Application.Services.Messages.Tabular;

public interface ISpreadsheetAnalysisEngine
{
    SpreadsheetAnalysisComputation Analyze(
        SpreadsheetWorkbook workbook,
        SpreadsheetAnalysisRequest request);
}
