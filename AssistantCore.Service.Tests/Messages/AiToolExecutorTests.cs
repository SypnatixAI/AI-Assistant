using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Tests.Messages;

public sealed class ToolExecutionRouterTests
{
    [Theory, AutoDomainData]
    public async Task Given_AMatchingHandler_When_ExecuteAsync_Then_MapsArgumentsAndReturnsHandlerResult(
        Guid callId,
        string query,
        RetrievedEvidence evidence)
    {
        // Given
        var expectedResult = ToolExecutionResult.Succeeded(callId.ToString(), [evidence]);
        var handler = new RecordingInternalDataHandler(expectedResult);
        var router = new ToolExecutionRouter([handler]);
        var validatedToolCall = CreateValidatedToolCall(callId, query);
        var executionContext = new ConnectorExecutionContext(Guid.NewGuid(), Guid.NewGuid());

        // When
        var result = await router.ExecuteAsync(
            validatedToolCall,
            executionContext,
            CancellationToken.None);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(query, handler.ReceivedArguments?.Query);
        Assert.Equal(callId.ToString(), handler.ReceivedToolCallId);
        Assert.Same(executionContext, handler.ReceivedExecutionContext);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoMatchingHandler_When_ExecuteAsync_Then_ReturnsControlledFailure(
        Guid callId,
        string query)
    {
        // Given
        var router = new ToolExecutionRouter([]);
        var validatedToolCall = CreateValidatedToolCall(callId, query);
        var executionContext = new ConnectorExecutionContext(Guid.NewGuid(), Guid.NewGuid());

        // When
        var result = await router.ExecuteAsync(
            validatedToolCall,
            executionContext,
            CancellationToken.None);

        // Then
        Assert.Equal(ToolExecutionStatus.Failed, result.Status);
        Assert.Equal(ToolExecutionErrorCodes.ExecutorNotFound, result.ErrorCode);
        Assert.Empty(result.Evidence);
    }

    [Theory, AutoDomainData]
    public async Task Given_MultipleMatchingHandlers_When_ExecuteAsync_Then_ReturnsControlledFailure(
        Guid callId,
        string query,
        RetrievedEvidence evidence)
    {
        // Given
        var handlerResult = ToolExecutionResult.Succeeded(callId.ToString(), [evidence]);
        var router = new ToolExecutionRouter(
            [
                new RecordingInternalDataHandler(handlerResult),
                new RecordingInternalDataHandler(handlerResult)
            ]);
        var validatedToolCall = CreateValidatedToolCall(callId, query);
        var executionContext = new ConnectorExecutionContext(Guid.NewGuid(), Guid.NewGuid());

        // When
        var result = await router.ExecuteAsync(
            validatedToolCall,
            executionContext,
            CancellationToken.None);

        // Then
        Assert.Equal(ToolExecutionStatus.Failed, result.Status);
        Assert.Equal(ToolExecutionErrorCodes.ExecutorAmbiguous, result.ErrorCode);
    }

    [Theory, AutoDomainData]
    public async Task Given_ArgumentsCannotBeMapped_When_ExecuteAsync_Then_ReturnsControlledFailure(
        Guid callId,
        RetrievedEvidence evidence)
    {
        // Given
        var handlerResult = ToolExecutionResult.Succeeded(callId.ToString(), [evidence]);
        var handler = new RecordingInternalDataHandler(handlerResult);
        var router = new ToolExecutionRouter([handler]);
        var validatedToolCall = new ValidatedToolCall(
            callId.ToString(),
            AiToolNames.SearchInternalData,
            JsonSerializer.SerializeToElement(new { query = 123 }));
        var executionContext = new ConnectorExecutionContext(Guid.NewGuid(), Guid.NewGuid());

        // When
        var result = await router.ExecuteAsync(
            validatedToolCall,
            executionContext,
            CancellationToken.None);

        // Then
        Assert.Equal(ToolExecutionStatus.Failed, result.Status);
        Assert.Equal(ToolExecutionErrorCodes.ArgumentMappingFailed, result.ErrorCode);
        Assert.Null(handler.ReceivedArguments);
    }

    [Theory, AutoDomainData]
    public async Task Given_ACancelledExecution_When_ExecuteAsync_Then_PropagatesCancellation(
        Guid callId,
        string query)
    {
        // Given
        var router = new ToolExecutionRouter([]);
        var validatedToolCall = CreateValidatedToolCall(callId, query);
        var executionContext = new ConnectorExecutionContext(Guid.NewGuid(), Guid.NewGuid());
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        // When
        var exception = await Record.ExceptionAsync(() =>
            router.ExecuteAsync(
                validatedToolCall,
                executionContext,
                cancellationSource.Token));

        // Then
        Assert.IsType<OperationCanceledException>(exception);
    }

    private static ValidatedToolCall CreateValidatedToolCall(Guid callId, string query) => new(
        callId.ToString(),
        AiToolNames.SearchInternalData,
        JsonSerializer.SerializeToElement(new { query }));

    private sealed class RecordingInternalDataHandler(ToolExecutionResult result)
        : AiToolExecutionHandler<SearchInternalDataToolArguments>(
            AiToolNames.SearchInternalData)
    {
        public string? ReceivedToolCallId { get; private set; }

        public SearchInternalDataToolArguments? ReceivedArguments { get; private set; }

        public ConnectorExecutionContext? ReceivedExecutionContext { get; private set; }

        protected override Task<ToolExecutionResult> ExecuteAsync(
            string toolCallId,
            SearchInternalDataToolArguments arguments,
            ConnectorExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            ReceivedToolCallId = toolCallId;
            ReceivedArguments = arguments;
            ReceivedExecutionContext = executionContext;
            return Task.FromResult(result);
        }
    }
}
