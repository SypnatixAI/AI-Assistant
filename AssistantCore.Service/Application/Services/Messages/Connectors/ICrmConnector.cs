using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;

namespace AssistantCore.Service.Application.Services.Messages.Connectors;

public interface ICrmConnector
{
    Task<ConnectorResult> SearchAsync(
        QueryCrmToolArguments request,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken);
}
