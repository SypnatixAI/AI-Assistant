using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Infrastructure.Connectors.InternalData;

namespace AssistantCore.Service.Tests.Messages;

public sealed class InternalDataToolExecutionHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_InternalDataEvidence_When_ExecuteAsync_Then_ReturnsSuccessfulToolResult(
        SearchInternalDataToolArguments arguments,
        ConnectorExecutionContext executionContext,
        RetrievedEvidence evidence)
    {
        // Given
        var connector = new StubInternalDataConnector(new ConnectorResult([evidence]));
        var handler = new InternalDataToolExecutionHandler(connector);
        var toolCall = new ValidatedToolCall(
            "tool-call-001",
            AiToolNames.SearchInternalData,
            JsonSerializer.SerializeToElement(arguments));

        // When
        var result = await handler.ExecuteAsync(
            toolCall,
            executionContext,
            CancellationToken.None);

        // Then
        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal([evidence], result.Evidence);
        Assert.Equal(arguments, connector.ReceivedRequest);
        Assert.Equal(executionContext, connector.ReceivedContext);
    }

    private sealed class StubInternalDataConnector(ConnectorResult result)
        : IInternalDataConnector
    {
        public SearchInternalDataToolArguments? ReceivedRequest { get; private set; }

        public ConnectorExecutionContext? ReceivedContext { get; private set; }

        public Task<ConnectorResult> SearchAsync(
            SearchInternalDataToolArguments request,
            ConnectorExecutionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedRequest = request;
            ReceivedContext = context;
            return Task.FromResult(result);
        }
    }
}
