using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public sealed class ToolCallBatchExecutor(
    IAiToolCallValidator toolCallValidator,
    IToolExecutionRouter toolExecutionRouter,
    IToolCallFingerprintGenerator fingerprintGenerator,
    IAiToolFailureWarningFactory failureWarningFactory,
    TimeProvider timeProvider) : IToolCallBatchExecutor
{
    public async Task<IReadOnlyCollection<ToolExecutionResult>> ExecuteAsync(
        MessageOrchestrationState state,
        IReadOnlyCollection<AiRequestedToolCall> requestedToolCalls,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(requestedToolCalls);
        cancellationToken.ThrowIfCancellationRequested();

        var maximumParallelCalls = state.Budget.Limits.MaximumParallelToolCalls;
        EnsurePositive(maximumParallelCalls, nameof(maximumParallelCalls));

        var validatedToolCalls = await ValidateAllAsync(
            requestedToolCalls,
            state.AllowedTools,
            cancellationToken);
        var toolCallFingerprints = requestedToolCalls
            .Select(fingerprintGenerator.CreateFingerprint)
            .ToArray();

        state.AcceptToolCalls(
            requestedToolCalls,
            toolCallFingerprints,
            timeProvider.GetUtcNow());

        var executionResults = new ToolExecutionResult[validatedToolCalls.Length];
        try
        {
            await ExecuteAllAsync(
                validatedToolCalls,
                executionResults,
                state.ToolExecutionContext,
                maximumParallelCalls,
                cancellationToken);
        }
        catch
        {
            state.RecordToolResults(executionResults.OfType<ToolExecutionResult>().ToArray());
            throw;
        }

        state.RecordToolResults(executionResults);
        return executionResults;
    }

    private async Task<ValidatedToolCall[]> ValidateAllAsync(
        IReadOnlyCollection<AiRequestedToolCall> requestedToolCalls,
        IReadOnlyCollection<AiToolDefinition> allowedTools,
        CancellationToken cancellationToken)
    {
        var validatedToolCalls = new List<ValidatedToolCall>(requestedToolCalls.Count);
        foreach (var requestedToolCall in requestedToolCalls)
        {
            validatedToolCalls.Add(await toolCallValidator.ValidateAsync(
                requestedToolCall,
                allowedTools,
                cancellationToken));
        }

        return validatedToolCalls.ToArray();
    }

    private async Task ExecuteAllAsync(
        IReadOnlyList<ValidatedToolCall> validatedToolCalls,
        ToolExecutionResult[] executionResults,
        ConnectorExecutionContext executionContext,
        int maximumParallelCalls,
        CancellationToken cancellationToken)
    {
        using var concurrencyGate = new SemaphoreSlim(maximumParallelCalls);

        var executionTasks = validatedToolCalls.Select((toolCall, index) =>
            ExecuteOneAsync(toolCall, index));
        await Task.WhenAll(executionTasks);

        async Task ExecuteOneAsync(ValidatedToolCall toolCall, int resultIndex)
        {
            await concurrencyGate.WaitAsync(cancellationToken);
            try
            {
                var result = await toolExecutionRouter.ExecuteAsync(
                    toolCall,
                    executionContext,
                    cancellationToken);
                EnsureMatchingCallId(toolCall, result);
                result = AddFailureWarningWhenMissing(toolCall, result);
                executionResults[resultIndex] = result;
            }
            finally
            {
                concurrencyGate.Release();
            }
        }
    }

    private ToolExecutionResult AddFailureWarningWhenMissing(
        ValidatedToolCall toolCall,
        ToolExecutionResult result)
    {
        if (result.Status != ToolExecutionStatus.Failed || result.Warnings.Count > 0)
        {
            return result;
        }

        return ToolExecutionResult.Failed(
            result.ToolCallId,
            result.ErrorCode
                ?? throw new InvalidOperationException(
                    "A failed tool result must contain an error code."),
            [failureWarningFactory.Create(toolCall.ToolName)]);
    }

    private static void EnsureMatchingCallId(
        ValidatedToolCall toolCall,
        ToolExecutionResult result)
    {
        if (!string.Equals(toolCall.CallId, result.ToolCallId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The tool execution result does not match the requested call identifier.");
        }
    }

    private static void EnsurePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "The configured limit must be greater than zero.");
        }
    }
}
