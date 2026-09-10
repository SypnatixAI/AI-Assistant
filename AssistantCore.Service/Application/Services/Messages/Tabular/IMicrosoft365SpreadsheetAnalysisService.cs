using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tabular;

namespace AssistantCore.Service.Application.Services.Messages.Tabular;

public interface IMicrosoft365SpreadsheetAnalysisService
{
    Task<SpreadsheetAnalysisResult> AnalyzeAsync(
        SpreadsheetAnalysisRequest request,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken);
}
