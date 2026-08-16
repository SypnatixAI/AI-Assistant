using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Tests.Messages;

public sealed class AiToolExecutorTests
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
        var executor = new AiToolExecutor([handler]);
        var validatedToolCall = CreateValidatedToolCall(callId, query);

        // When
        var result = await executor.ExecuteAsync(validatedToolCall, CancellationToken.None);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(query, handler.ReceivedArguments?.Query);
        Assert.Equal(callId.ToString(), handler.ReceivedToolCallId);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoMatchingHandler_When_ExecuteAsync_Then_ReturnsControlledFailure(
        Guid callId,
        string query)
    {
        // Given
        var executor = new AiToolExecutor([]);
        var validatedToolCall = CreateValidatedToolCall(callId, query);

        // When
        var result = await executor.ExecuteAsync(validatedToolCall, CancellationToken.None);

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
        var executor = new AiToolExecutor(
            [
                new RecordingInternalDataHandler(handlerResult),
                new RecordingInternalDataHandler(handlerResult)
            ]);
        var validatedToolCall = CreateValidatedToolCall(callId, query);

        // When
        var result = await executor.ExecuteAsync(validatedToolCall, CancellationToken.None);

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
        var executor = new AiToolExecutor([handler]);
        var validatedToolCall = new ValidatedToolCall(
            callId.ToString(),
            AiToolNames.SearchInternalData,
            JsonSerializer.SerializeToElement(new { query = 123 }));

        // When
        var result = await executor.ExecuteAsync(validatedToolCall, CancellationToken.None);

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
        var executor = new AiToolExecutor([]);
        var validatedToolCall = CreateValidatedToolCall(callId, query);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        // When
        var exception = await Record.ExceptionAsync(() =>
            executor.ExecuteAsync(validatedToolCall, cancellationSource.Token));

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

        protected override Task<ToolExecutionResult> ExecuteAsync(
            string toolCallId,
            SearchInternalDataToolArguments arguments,
            CancellationToken cancellationToken)
        {
            ReceivedToolCallId = toolCallId;
            ReceivedArguments = arguments;
            return Task.FromResult(result);
        }
    }
}
