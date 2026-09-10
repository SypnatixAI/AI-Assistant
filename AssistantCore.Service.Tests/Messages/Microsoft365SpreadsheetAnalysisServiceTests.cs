using System.Text.Json;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tabular;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Tabular;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Messages;

public sealed class Microsoft365SpreadsheetAnalysisServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_AggregateAndFilters_When_AnalyzeAsync_Then_CalculatesAgainstEveryRow(
        string fileName)
    {
        // Given
        fileName = $"{fileName}.xlsx";
        var document = new Microsoft365SpreadsheetDocument(
            fileName,
            "tenant-id",
            "drive-id",
            "item-id",
            "document-reference",
            "https://contoso.sharepoint.com/workbook.xlsx");
        var rows = new IReadOnlyDictionary<string, string>[]
        {
            Row("1", "100", "3.0", "0.20", "0.80"),
            Row("2", "200", "2.0", "0.10", "0.70"),
            Row("3", "600", "3.2", "0.25", "0.85")
        };
        var service = new Microsoft365SpreadsheetAnalysisService(
            new StubDocumentResolver(document),
            new StubContentClient(),
            new StubWorkbookReader(new SpreadsheetWorkbook(
            [
                new SpreadsheetWorksheet(
                    "Transactions",
                    ["Transaction ID", "Transaction Amount", "Debt-to-Equity Ratio", "Profit Margin", "Accuracy Score"],
                    rows)
            ])),
            new SpreadsheetAnalysisEngine());
        var request = new SpreadsheetAnalysisRequest(
            fileName,
            "Transactions",
            [new SpreadsheetAggregationArguments("averageAmount", "average", "Transaction Amount")],
            [
                FilterAgainstAggregation("Transaction Amount", "greater_than", "averageAmount"),
                NumericFilter("Debt-to-Equity Ratio", "greater_than", 2.5m),
                NumericFilter("Profit Margin", "less_than", 0.30m),
                NumericFilter("Accuracy Score", "less_than", 0.90m)
            ],
            ["Transaction ID"]);

        // When
        var result = await service.AnalyzeAsync(
            request,
            CreateExecutionContext(),
            CancellationToken.None);

        // Then
        Assert.Equal(300m, result.Aggregations["averageAmount"]);
        Assert.Equal(3, result.TotalRowCount);
        Assert.Equal(1, result.MatchingRowCount);
        Assert.Equal("3", Assert.Single(result.Rows)["Transaction ID"]);
        Assert.False(result.RowsTruncated);
    }

    private static IReadOnlyDictionary<string, string> Row(
        string id,
        string amount,
        string debtToEquity,
        string profitMargin,
        string accuracyScore) => new Dictionary<string, string>
        {
            ["Transaction ID"] = id,
            ["Transaction Amount"] = amount,
            ["Debt-to-Equity Ratio"] = debtToEquity,
            ["Profit Margin"] = profitMargin,
            ["Accuracy Score"] = accuracyScore
        };

    private static SpreadsheetFilterArguments FilterAgainstAggregation(
        string column,
        string filterOperator,
        string alias) => new(
            column,
            filterOperator,
            Value: null,
            AggregationAlias: alias);

    private static SpreadsheetFilterArguments NumericFilter(
        string column,
        string filterOperator,
        decimal value) => new(
            column,
            filterOperator,
            JsonSerializer.SerializeToElement(value),
            AggregationAlias: null);

    private static ConnectorExecutionContext CreateExecutionContext() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "tenant-id",
        Guid.NewGuid(),
        IdentityProvider.MicrosoftEntraId,
        UserEmail: "member@contoso.com");

    private sealed class StubDocumentResolver(Microsoft365SpreadsheetDocument document)
        : IMicrosoft365SpreadsheetDocumentResolver
    {
        public Task<Microsoft365SpreadsheetDocument> ResolveAsync(
            string fileName,
            ConnectorExecutionContext context,
            CancellationToken cancellationToken) => Task.FromResult(document);
    }

    private sealed class StubContentClient : IMicrosoft365DriveContentClient
    {
        public Task<byte[]> DownloadAsync(
            string tenantId,
            string driveId,
            string driveItemId,
            CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<byte>());
    }

    private sealed class StubWorkbookReader(SpreadsheetWorkbook workbook) : ISpreadsheetWorkbookReader
    {
        public Task<SpreadsheetWorkbook> ReadAsync(
            string fileName,
            byte[] content,
            CancellationToken cancellationToken) => Task.FromResult(workbook);
    }
}
