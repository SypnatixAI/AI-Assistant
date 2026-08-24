using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;

namespace AssistantCore.Service.Application.Services.Messages.Connectors;

public interface IMicrosoft365Connector
{
    Task<ConnectorResult> SearchAsync(
        SearchMicrosoft365ToolArguments request,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken);
}
