using System.Text.Json;

namespace AssistantCore.Service.Application.Models.Messages.Tools.Arguments;

public sealed record AnalyzeMicrosoft365SpreadsheetToolArguments(
    string FileName,
    string? WorksheetName,
    IReadOnlyCollection<SpreadsheetAggregationArguments>? Aggregations,
    IReadOnlyCollection<SpreadsheetFilterArguments>? Filters,
    IReadOnlyCollection<string>? SelectColumns);

public sealed record SpreadsheetAggregationArguments(
    string Alias,
    string Operation,
    string Column);

public sealed record SpreadsheetFilterArguments(
    string Column,
    string Operator,
    JsonElement? Value,
    string? AggregationAlias);
