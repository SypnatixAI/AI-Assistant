using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SearchToolExecutionHandlerTests
{
    [Theory, InlineAutoDomainData("et lequel est le plus risqué ?")]
    public async Task Given_RetrievalTimesOut_When_ExecuteAsync_Then_ReturnsFailedToolResult(
        string query,
        string toolCallId)
    {
        // Given
        var handler = new Microsoft365SearchToolExecutionHandler(
            new TimeoutMicrosoft365Connector());
        var arguments = JsonSerializer.SerializeToElement(
            new SearchMicrosoft365ToolArguments(query, null, null, null),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var toolCall = new ValidatedToolCall(
            toolCallId,
            AiToolNames.SearchMicrosoft365,
            arguments);

        // When
        var result = await handler.ExecuteAsync(
            toolCall,
            new ConnectorExecutionContext(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        // Then
        Assert.Equal(ToolExecutionStatus.Failed, result.Status);
        Assert.Equal(ToolExecutionErrorCodes.EnterpriseSearchTimeout, result.ErrorCode);
    }

    private sealed class TimeoutMicrosoft365Connector : IMicrosoft365Connector
    {
        public Task<ConnectorResult> SearchAsync(
            SearchMicrosoft365ToolArguments request,
            ConnectorExecutionContext context,
            CancellationToken cancellationToken) =>
            throw new TaskCanceledException("Knowledge base retrieval timed out.");
    }
}
