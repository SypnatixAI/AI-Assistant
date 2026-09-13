using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;

namespace AssistantCore.Service.Application.Models.Messages.Tabular;

public sealed record Microsoft365SpreadsheetDocument(
    string FileName,
    string TenantId,
    string DriveId,
    string DriveItemId,
    string Reference,
    string? Url);

public sealed record SpreadsheetWorkbook(
    IReadOnlyCollection<SpreadsheetWorksheet> Worksheets);

public sealed record SpreadsheetWorksheet(
    string Name,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

public sealed record SpreadsheetAnalysisResult(
    string FileName,
    string WorksheetName,
    int TotalRowCount,
    IReadOnlyDictionary<string, decimal> Aggregations,
    int MatchingRowCount,
    IReadOnlyCollection<IReadOnlyDictionary<string, string>> Rows,
    bool RowsTruncated,
    string Reference,
    string? Url)
{
    public string ToJson() => JsonSerializer.Serialize(new
    {
        fileName = FileName,
        worksheetName = WorksheetName,
        totalRowCount = TotalRowCount,
        aggregations = Aggregations,
        matchingRowCount = MatchingRowCount,
        rows = Rows,
        rowsTruncated = RowsTruncated
    });
}

public sealed record SpreadsheetAnalysisComputation(
    string WorksheetName,
    int TotalRowCount,
    IReadOnlyDictionary<string, decimal> Aggregations,
    int MatchingRowCount,
    IReadOnlyCollection<IReadOnlyDictionary<string, string>> Rows,
    bool RowsTruncated);

public sealed record SpreadsheetAnalysisRequest(
    string FileName,
    string? WorksheetName,
    IReadOnlyCollection<SpreadsheetAggregationArguments> Aggregations,
    IReadOnlyCollection<SpreadsheetFilterArguments> Filters,
    IReadOnlyCollection<string>? SelectColumns);
