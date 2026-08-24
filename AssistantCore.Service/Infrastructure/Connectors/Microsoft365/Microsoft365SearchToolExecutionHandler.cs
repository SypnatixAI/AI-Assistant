using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

public sealed class Microsoft365SearchToolExecutionHandler(IMicrosoft365Connector connector)
    : AiToolExecutionHandler<SearchMicrosoft365ToolArguments>(AiToolNames.SearchMicrosoft365)
{
    protected override async Task<ToolExecutionResult> ExecuteAsync(
        string toolCallId,
        SearchMicrosoft365ToolArguments arguments,
        ConnectorExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        var result = await connector.SearchAsync(arguments, executionContext, cancellationToken);
        return ToolExecutionResult.Succeeded(toolCallId, result.Evidence);
    }
}
