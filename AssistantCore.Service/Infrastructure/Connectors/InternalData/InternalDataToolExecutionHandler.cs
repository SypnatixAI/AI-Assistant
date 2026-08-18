using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Infrastructure.Connectors.InternalData;

public sealed class InternalDataToolExecutionHandler(
    IInternalDataConnector connector)
    : AiToolExecutionHandler<SearchInternalDataToolArguments>(
        AiToolNames.SearchInternalData)
{
    protected override async Task<ToolExecutionResult> ExecuteAsync(
        string toolCallId,
        SearchInternalDataToolArguments arguments,
        ConnectorExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        var result = await connector.SearchAsync(
            arguments,
            executionContext,
            cancellationToken);

        return ToolExecutionResult.Succeeded(toolCallId, result.Evidence);
    }
}
