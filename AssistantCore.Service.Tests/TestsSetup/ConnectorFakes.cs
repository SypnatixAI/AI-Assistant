using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors;

namespace AssistantCore.Service.Tests;

internal sealed class FakeErpConnector : IErpConnector
{
    public ConnectorResult Result { get; init; } = new([]);

    public QueryErpToolArguments? ReceivedRequest { get; private set; }

    public ConnectorExecutionContext? ReceivedContext { get; private set; }

    public Task<ConnectorResult> ReadAsync(
        QueryErpToolArguments request,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReceivedRequest = request;
        ReceivedContext = context;
        return Task.FromResult(Result);
    }
}

internal sealed class FakeCrmConnector : ICrmConnector
{
    public ConnectorResult Result { get; init; } = new([]);

    public QueryCrmToolArguments? ReceivedRequest { get; private set; }

    public ConnectorExecutionContext? ReceivedContext { get; private set; }

    public Task<ConnectorResult> SearchAsync(
        QueryCrmToolArguments request,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReceivedRequest = request;
        ReceivedContext = context;
        return Task.FromResult(Result);
    }
}
