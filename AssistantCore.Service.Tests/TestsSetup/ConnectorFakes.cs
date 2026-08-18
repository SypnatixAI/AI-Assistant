using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Application.Services.Messages.Tools;

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

internal sealed class FakeToolExecutionHandler(string toolName) : IAiToolExecutionHandler
{
    public string ToolName { get; } = toolName;

    public Task<ToolExecutionResult> ExecuteAsync(
        ValidatedToolCall validatedToolCall,
        ConnectorExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ToolExecutionResult.Failed(
            validatedToolCall.CallId,
            ToolExecutionErrorCodes.ExecutorNotFound));
    }
}
