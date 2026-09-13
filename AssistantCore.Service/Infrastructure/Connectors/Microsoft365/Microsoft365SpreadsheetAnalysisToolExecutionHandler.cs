using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tabular;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Tabular;
using AssistantCore.Service.Application.Services.Messages.Tools;
using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

public sealed class Microsoft365SpreadsheetAnalysisToolExecutionHandler(
    IMicrosoft365SpreadsheetAnalysisService analysisService,
    ILogger<Microsoft365SpreadsheetAnalysisToolExecutionHandler> logger)
    : AiToolExecutionHandler<AnalyzeMicrosoft365SpreadsheetToolArguments>(
        AiToolNames.AnalyzeMicrosoft365Spreadsheet)
{
    protected override async Task<ToolExecutionResult> ExecuteAsync(
        string toolCallId,
        AnalyzeMicrosoft365SpreadsheetToolArguments arguments,
        ConnectorExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await analysisService.AnalyzeAsync(
                new SpreadsheetAnalysisRequest(
                    arguments.FileName,
                    arguments.WorksheetName,
                    arguments.Aggregations ?? [],
                    arguments.Filters ?? [],
                    arguments.SelectColumns),
                executionContext,
                cancellationToken);
            var evidence = new RetrievedEvidence(
                $"spreadsheet-analysis:{result.Reference}",
                "Microsoft365Spreadsheet",
                result.FileName,
                result.ToJson(),
                result.Reference,
                result.Url,
                OccurredAt: null);

            return result.RowsTruncated
                ? ToolExecutionResult.PartiallySucceeded(
                    toolCallId,
                    [evidence],
                    ["The matching row count is exact, but the returned rows were limited to 200 rows or 40,000 characters."])
                : ToolExecutionResult.Succeeded(toolCallId, [evidence]);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ToolExecutionResult.Failed(
                toolCallId,
                ToolExecutionErrorCodes.SpreadsheetAnalysisTimeout,
                ["Spreadsheet analysis timed out."]);
        }
        catch (Exception exception) when (exception is
            ArgumentException or InvalidOperationException or InvalidDataException or Microsoft365ExternalException)
        {
            logger.LogWarning(exception, "Microsoft 365 spreadsheet analysis failed.");
            return ToolExecutionResult.Failed(
                toolCallId,
                ToolExecutionErrorCodes.SpreadsheetAnalysisFailed,
                [CreateWarning(exception)]);
        }
    }

    private static string CreateWarning(Exception exception)
    {
        const int maximumLength = 1000;
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? "Spreadsheet analysis failed."
            : exception.Message.Trim();
        return message[..Math.Min(message.Length, maximumLength)];
    }
}
