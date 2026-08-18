using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;

namespace AssistantCore.Service.Application.Services.Messages.Connectors;

public interface IErpConnector
{
    Task<ConnectorResult> ReadAsync(
        QueryErpToolArguments request,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken);
}
