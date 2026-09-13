using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Application.Services.Messages.Tools;
using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

public sealed class Microsoft365SearchToolExecutionHandler(
    IMicrosoft365Connector connector,
    ILogger<Microsoft365SearchToolExecutionHandler> logger)
    : AiToolExecutionHandler<SearchMicrosoft365ToolArguments>(AiToolNames.SearchMicrosoft365)
{
    protected override async Task<ToolExecutionResult> ExecuteAsync(
        string toolCallId,
        SearchMicrosoft365ToolArguments arguments,
        ConnectorExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await connector.SearchAsync(arguments, executionContext, cancellationToken);
            return ToolExecutionResult.Succeeded(toolCallId, result.Evidence);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ToolExecutionResult.Failed(
                toolCallId,
                ToolExecutionErrorCodes.EnterpriseSearchTimeout,
                ["Microsoft 365 retrieval timed out."]);
        }
        catch (Microsoft365ExternalException exception)
        {
            logger.LogError(
                exception,
                "Microsoft 365 enterprise search failed while consulting Azure AI Search.");

            return ToolExecutionResult.Failed(
                toolCallId,
                ToolExecutionErrorCodes.EnterpriseSearchUnavailable,
                ["Microsoft 365 could not be consulted."]);
        }
    }
}
