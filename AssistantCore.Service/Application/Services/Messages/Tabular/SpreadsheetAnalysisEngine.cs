using System.Globalization;
using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages.Tabular;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;

namespace AssistantCore.Service.Application.Services.Messages.Tabular;

public sealed class SpreadsheetAnalysisEngine : ISpreadsheetAnalysisEngine
{
    private const int MaximumReturnedRows = 200;
    private const int MaximumReturnedCharacters = 40_000;
    private static readonly IReadOnlySet<string> SupportedAggregationOperations =
        new HashSet<string>(["average", "sum", "minimum", "maximum", "count"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> SupportedFilterOperators =
        new HashSet<string>(
            ["equals", "not_equals", "greater_than", "greater_than_or_equal", "less_than", "less_than_or_equal", "contains"],
            StringComparer.Ordinal);

    public SpreadsheetAnalysisComputation Analyze(
        SpreadsheetWorkbook workbook,
        SpreadsheetAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var worksheet = ResolveWorksheet(workbook, request.WorksheetName);
        var columns = worksheet.Columns.ToDictionary(column => column, StringComparer.OrdinalIgnoreCase);
        EnsureColumnsExist(columns, request);
        var aggregations = CalculateAggregations(worksheet.Rows, request.Aggregations, columns);
        var matchingRows = worksheet.Rows
            .Where(row => MatchesAllFilters(row, request.Filters, aggregations, columns))
            .ToArray();
        var selectedRows = SelectRows(matchingRows, request.SelectColumns, columns);

        return new SpreadsheetAnalysisComputation(
            worksheet.Name,
            worksheet.Rows.Count,
            aggregations,
            matchingRows.Length,
            selectedRows.Rows,
            selectedRows.IsTruncated);
    }

    private static void ValidateRequest(SpreadsheetAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Aggregations);
        ArgumentNullException.ThrowIfNull(request.Filters);

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var aggregation in request.Aggregations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(aggregation.Alias);
            ArgumentException.ThrowIfNullOrWhiteSpace(aggregation.Column);
            var operation = Normalize(aggregation.Operation);
            if (!SupportedAggregationOperations.Contains(operation))
            {
                throw new ArgumentException($"Unsupported spreadsheet aggregation '{aggregation.Operation}'.");
            }

            if (!aliases.Add(aggregation.Alias))
            {
                throw new ArgumentException($"Duplicate spreadsheet aggregation alias '{aggregation.Alias}'.");
            }
        }

        foreach (var filter in request.Filters)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filter.Column);
            var filterOperator = Normalize(filter.Operator);
            if (!SupportedFilterOperators.Contains(filterOperator))
            {
                throw new ArgumentException($"Unsupported spreadsheet filter operator '{filter.Operator}'.");
            }

            var hasValue = filter.Value is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined };
            var hasAggregation = !string.IsNullOrWhiteSpace(filter.AggregationAlias);
            if (hasValue == hasAggregation)
            {
                throw new ArgumentException("A spreadsheet filter must use exactly one literal value or aggregation alias.");
            }

            if (hasAggregation && !aliases.Contains(filter.AggregationAlias!))
            {
                throw new ArgumentException($"Unknown spreadsheet aggregation alias '{filter.AggregationAlias}'.");
            }
        }
    }

    private static SpreadsheetWorksheet ResolveWorksheet(
        SpreadsheetWorkbook workbook,
        string? worksheetName)
    {
        if (string.IsNullOrWhiteSpace(worksheetName))
        {
            return workbook.Worksheets.Count == 1
                ? workbook.Worksheets.Single()
                : throw new InvalidOperationException(
                    "The workbook contains multiple worksheets; worksheetName is required.");
        }

        return workbook.Worksheets.SingleOrDefault(worksheet =>
                   string.Equals(worksheet.Name, worksheetName, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"Worksheet '{worksheetName}' was not found.");
    }

    private static void EnsureColumnsExist(
        IReadOnlyDictionary<string, string> columns,
        SpreadsheetAnalysisRequest request)
    {
        var requestedColumns = request.Aggregations.Select(item => item.Column)
            .Concat(request.Filters.Select(item => item.Column))
            .Concat(request.SelectColumns ?? []);
        var missingColumn = requestedColumns.FirstOrDefault(column => !columns.ContainsKey(column));
        if (missingColumn is not null)
        {
            throw new InvalidOperationException($"Column '{missingColumn}' was not found in the worksheet.");
        }
    }

    private static IReadOnlyDictionary<string, decimal> CalculateAggregations(
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        IReadOnlyCollection<SpreadsheetAggregationArguments> requests,
        IReadOnlyDictionary<string, string> columns)
    {
        var results = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in requests)
        {
            var rawValues = rows
                .Select(row => row[columns[request.Column]])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            var operation = Normalize(request.Operation);
            if (operation == "count")
            {
                results.Add(request.Alias, rawValues.Length);
                continue;
            }

            var values = rawValues.Select(ParseDecimal).ToArray();
            var result = operation switch
            {
                "sum" => values.Sum(),
                "average" when values.Length > 0 => values.Average(),
                "minimum" when values.Length > 0 => values.Min(),
                "maximum" when values.Length > 0 => values.Max(),
                _ => throw new InvalidOperationException(
                    $"Aggregation '{request.Alias}' has no numeric value to process.")
            };
            results.Add(request.Alias, result);
        }

        return results;
    }

    private static bool MatchesAllFilters(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyCollection<SpreadsheetFilterArguments> filters,
        IReadOnlyDictionary<string, decimal> aggregations,
        IReadOnlyDictionary<string, string> columns) =>
        filters.All(filter => MatchesFilter(row[columns[filter.Column]], filter, aggregations));

    private static bool MatchesFilter(
        string actual,
        SpreadsheetFilterArguments filter,
        IReadOnlyDictionary<string, decimal> aggregations)
    {
        var expected = !string.IsNullOrWhiteSpace(filter.AggregationAlias)
            ? aggregations[filter.AggregationAlias!].ToString(CultureInfo.InvariantCulture)
            : GetLiteralValue(filter.Value!.Value);
        var filterOperator = Normalize(filter.Operator);

        if (filterOperator == "contains")
        {
            return actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
        }

        if (decimal.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var actualNumber)
            && decimal.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var expectedNumber))
        {
            return Compare(actualNumber.CompareTo(expectedNumber), filterOperator);
        }

        if (filterOperator is not "equals" and not "not_equals")
        {
            throw new InvalidOperationException(
                $"Column '{filter.Column}' contains a value that cannot be compared numerically.");
        }

        return Compare(string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase), filterOperator);
    }

    private static bool Compare(int comparison, string filterOperator) => filterOperator switch
    {
        "equals" => comparison == 0,
        "not_equals" => comparison != 0,
        "greater_than" => comparison > 0,
        "greater_than_or_equal" => comparison >= 0,
        "less_than" => comparison < 0,
        "less_than_or_equal" => comparison <= 0,
        _ => false
    };

    private static SelectedRows SelectRows(
        IReadOnlyCollection<IReadOnlyDictionary<string, string>> matchingRows,
        IReadOnlyCollection<string>? requestedColumns,
        IReadOnlyDictionary<string, string> columns)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();
        var characterCount = 0;
        foreach (var matchingRow in matchingRows.Take(MaximumReturnedRows))
        {
            var selectedRow = SelectColumns(matchingRow, requestedColumns, columns);
            var serializedLength = JsonSerializer.Serialize(selectedRow).Length;
            if (serializedLength > MaximumReturnedCharacters - characterCount)
            {
                break;
            }

            rows.Add(selectedRow);
            characterCount += serializedLength;
        }

        return new SelectedRows(rows, rows.Count < matchingRows.Count);
    }

    private static IReadOnlyDictionary<string, string> SelectColumns(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyCollection<string>? requestedColumns,
        IReadOnlyDictionary<string, string> columns)
    {
        if (requestedColumns is null || requestedColumns.Count == 0)
        {
            return row.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        return requestedColumns
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                column => columns[column],
                column => row[columns[column]],
                StringComparer.OrdinalIgnoreCase);
    }

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidOperationException("A requested aggregation contains a non-numeric value.");

    private static string GetLiteralValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => bool.TrueString,
        JsonValueKind.False => bool.FalseString,
        _ => throw new ArgumentException("Spreadsheet filter values must be strings, numbers or booleans.")
    };

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private sealed record SelectedRows(
        IReadOnlyCollection<IReadOnlyDictionary<string, string>> Rows,
        bool IsTruncated);
}
