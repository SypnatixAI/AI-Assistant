using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;

namespace AssistantCore.Service.Application.Services.Messages.Connectors;

public interface IInternalDataConnector
{
    Task<ConnectorResult> SearchAsync(
        SearchInternalDataToolArguments request,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken);
}
