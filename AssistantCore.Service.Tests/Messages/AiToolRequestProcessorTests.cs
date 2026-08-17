using System.Collections.Concurrent;
using System.Text.Json;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Tests.Messages;

public sealed class ToolCallBatchExecutorTests
{
    [Theory, AutoDomainData]
    public async Task Given_ValidIndependentCalls_When_ExecuteAsync_Then_ExecutesInParallelAndAggregatesResults(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var calls = CreateCalls(4);
        var sharedEvidence = CreateEvidence("evidence-shared");
        var executor = new RecordingToolExecutor(async (call, _, cancellationToken) =>
        {
            await Task.Delay(20, cancellationToken);
            return ToolExecutionResult.PartiallySucceeded(
                call.CallId,
                [sharedEvidence, CreateEvidence($"evidence-{call.CallId}")],
                [$"warning-{call.CallId}"]);
        });
        var state = CreateState(
            processing,
            now,
            maximumToolCalls: 10,
            maximumRepeatedToolCalls: 0,
            maximumParallelToolCalls: 2,
            maximumResultsPerTool: 1);
        var processor = CreateProcessor(executor, now);

        // When
        var results = await processor.ExecuteAsync(state, calls, CancellationToken.None);

        // Then
        Assert.Equal(calls.Select(call => call.CallId), results.Select(result => result.ToolCallId));
        Assert.Equal(2, executor.MaximumObservedConcurrency);
        Assert.All(executor.ReceivedContexts, context =>
        {
            Assert.Equal(processing.OrganizationId, context.OrganizationId);
            Assert.Equal(processing.OwnerMemberId, context.MemberId);
        });
        Assert.All(results, result => Assert.Single(result.Evidence));
        Assert.Single(state.CollectedEvidence);
        Assert.Equal(4, state.Warnings.Count);
        Assert.Equal(4, state.RequestedToolCalls.Count);
        Assert.Equal(4, state.ToolResults.Count);
        Assert.Equal(4, state.Budget.Usage.ToolCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInvalidCallInTheBatch_When_ExecuteAsync_Then_DoesNotExecuteAnyTool(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var calls = CreateCalls(2);
        var validator = new RecordingToolCallValidator(call =>
            call.CallId == calls.Last().CallId
                ? throw new ToolCallValidationException(call.CallId, "Rejected call.")
                : CreateValidatedCall(call));
        var executor = new RecordingToolExecutor((call, _, _) =>
            Task.FromResult(ToolExecutionResult.Succeeded(call.CallId, [])));
        var state = CreateState(processing, now);
        var processor = CreateProcessor(executor, now, validator);

        // When
        var exception = await Record.ExceptionAsync(() =>
            processor.ExecuteAsync(state, calls, CancellationToken.None));

        // Then
        Assert.IsType<ToolCallValidationException>(exception);
        Assert.Equal(0, executor.ExecutionCount);
        Assert.Empty(state.RequestedToolCalls);
    }

    [Theory, AutoDomainData]
    public async Task Given_InsufficientToolCallBudget_When_ExecuteAsync_Then_DoesNotExecuteAnyTool(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var calls = CreateCalls(2);
        var executor = new RecordingToolExecutor((call, _, _) =>
            Task.FromResult(ToolExecutionResult.Succeeded(call.CallId, [])));
        var state = CreateState(processing, now, maximumToolCalls: 1);
        var processor = CreateProcessor(executor, now);

        // When
        var exception = await Assert.ThrowsAsync<OrchestrationBudgetExceededException>(() =>
            processor.ExecuteAsync(state, calls, CancellationToken.None));

        // Then
        Assert.Equal(OrchestrationBudgetType.ToolCalls, exception.ExceededBudget);
        Assert.Equal(0, executor.ExecutionCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnExpiredExecutionBudget_When_ExecuteAsync_Then_DoesNotExecuteTheTool(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var executor = new RecordingToolExecutor((call, _, _) =>
            Task.FromResult(ToolExecutionResult.Succeeded(call.CallId, [])));
        var state = CreateState(
            processing,
            now,
            maximumExecutionTime: TimeSpan.FromSeconds(1));
        var processor = CreateProcessor(executor, now.AddSeconds(1));

        // When
        var exception = await Assert.ThrowsAsync<OrchestrationBudgetExceededException>(() =>
            processor.ExecuteAsync(state, CreateCalls(1), CancellationToken.None));

        // Then
        Assert.Equal(OrchestrationBudgetType.ExecutionTime, exception.ExceededBudget);
        Assert.Equal(0, executor.ExecutionCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_ARepeatedCallBeyondTheBudget_When_ExecuteAsync_Then_RejectsTheRepeatedCall(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var call = Assert.Single(CreateCalls(1));
        var executor = new RecordingToolExecutor((validatedCall, _, _) =>
            Task.FromResult(ToolExecutionResult.Succeeded(validatedCall.CallId, [])));
        var state = CreateState(
            processing,
            now,
            maximumToolCalls: 2,
            maximumRepeatedToolCalls: 0);
        var processor = CreateProcessor(executor, now);
        await processor.ExecuteAsync(state, [call], CancellationToken.None);

        // When
        var exception = await Assert.ThrowsAsync<OrchestrationBudgetExceededException>(() =>
            processor.ExecuteAsync(state, [call], CancellationToken.None));

        // Then
        Assert.Equal(OrchestrationBudgetType.RepeatedToolCalls, exception.ExceededBudget);
        Assert.Equal(1, executor.ExecutionCount);
    }

    [Theory]
    [InlineAutoDomainData(OrchestrationBudgetType.ModelTokens, 15, 2, 100)]
    [InlineAutoDomainData(OrchestrationBudgetType.EstimatedCost, 100, 1, 100)]
    [InlineAutoDomainData(OrchestrationBudgetType.ContextSize, 100, 2, 10)]
    public async Task Given_AConsumedModelBudget_When_ExecuteAsync_Then_DoesNotExecuteTheTool(
        OrchestrationBudgetType expectedExceededBudget,
        int maximumModelTokens,
        int maximumEstimatedCost,
        int maximumContextSize,
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var executor = new RecordingToolExecutor((call, _, _) =>
            Task.FromResult(ToolExecutionResult.Succeeded(call.CallId, [])));
        var state = CreateState(
            processing,
            now,
            maximumModelTokens: maximumModelTokens,
            maximumEstimatedCost: maximumEstimatedCost,
            maximumContextSize: maximumContextSize);
        state.Budget.RecordModelUsage(
            new AiModelUsage(10, 5, 1, 0, EstimatedCost: 1m),
            now);
        var processor = CreateProcessor(executor, now);

        // When
        var exception = await Assert.ThrowsAsync<OrchestrationBudgetExceededException>(() =>
            processor.ExecuteAsync(state, CreateCalls(1), CancellationToken.None));

        // Then
        Assert.Equal(expectedExceededBudget, exception.ExceededBudget);
        Assert.Equal(0, executor.ExecutionCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AFailedToolWithoutWarning_When_ExecuteAsync_Then_AddsASourceWarning(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var executor = new RecordingToolExecutor((call, _, _) =>
            Task.FromResult(ToolExecutionResult.Failed(call.CallId, "SOURCE_UNAVAILABLE")));
        var state = CreateState(processing, now);
        var processor = CreateProcessor(executor, now);

        // When
        var results = await processor.ExecuteAsync(
            state,
            CreateCalls(1),
            CancellationToken.None);

        // Then
        var result = Assert.Single(results);
        Assert.Equal(ToolExecutionStatus.Failed, result.Status);
        Assert.Equal(["Internal data could not be consulted."], result.Warnings);
        Assert.Equal(result.Warnings, state.Warnings);
    }

    private static ToolCallBatchExecutor CreateProcessor(
        RecordingToolExecutor executor,
        DateTimeOffset now,
        IAiToolCallValidator? validator = null) =>
        new(
            validator ?? new RecordingToolCallValidator(CreateValidatedCall),
            executor,
            new ToolCallFingerprintGenerator(),
            new AiToolFailureWarningFactory(),
            new StubTimeProvider(now));

    private static MessageOrchestrationState CreateState(
        StartedMessageProcessing processing,
        DateTimeOffset now,
        int maximumToolCalls = 10,
        int maximumRepeatedToolCalls = 1,
        int maximumParallelToolCalls = 2,
        int maximumResultsPerTool = 10,
        TimeSpan? maximumExecutionTime = null,
        int maximumModelTokens = 12_000,
        decimal maximumEstimatedCost = 1.25m,
        int maximumContextSize = 30_000)
    {
        var tools = new[]
        {
            new AiToolDefinition(
                AiToolNames.SearchInternalData,
                "Search internal data.",
                JsonSerializer.SerializeToElement(new { type = "object" }))
        };
        var limits = new OrchestrationExecutionLimits(
            maximumExecutionTime ?? TimeSpan.FromMinutes(2),
            maximumToolCalls,
            maximumModelTokens,
            maximumEstimatedCost,
            maximumResultsPerTool,
            maximumContextSize,
            maximumRepeatedToolCalls,
            maximumParallelToolCalls);

        return MessageOrchestrationState.Start(
            processing,
            new("OpenAI", "gpt-5.6-luna"),
            [],
            tools,
            limits,
            now);
    }

    private static IReadOnlyList<AiRequestedToolCall> CreateCalls(int count) =>
        Enumerable.Range(1, count)
            .Select(index => new AiRequestedToolCall(
                $"call-{index}",
                AiToolNames.SearchInternalData,
                JsonSerializer.SerializeToElement(new { query = $"query-{index}" })))
            .ToArray();

    private static ValidatedToolCall CreateValidatedCall(AiRequestedToolCall call) =>
        new(call.CallId, call.ToolName, call.Arguments.Clone());

    private static RetrievedEvidence CreateEvidence(string evidenceId) => new(
        evidenceId,
        "Internal",
        "Title",
        "Content",
        evidenceId,
        Url: null,
        OccurredAt: null);

    private sealed class RecordingToolCallValidator(
        Func<AiRequestedToolCall, ValidatedToolCall> validate) : IAiToolCallValidator
    {
        public Task<ValidatedToolCall> ValidateAsync(
            AiRequestedToolCall requestedToolCall,
            IReadOnlyCollection<AiToolDefinition> availableTools,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(validate(requestedToolCall));
        }
    }

    private sealed class RecordingToolExecutor(
        Func<ValidatedToolCall, ConnectorExecutionContext, CancellationToken,
            Task<ToolExecutionResult>> execute) : IToolExecutionRouter
    {
        private int _activeExecutions;
        private int _executionCount;
        private int _maximumObservedConcurrency;
        private readonly ConcurrentQueue<ConnectorExecutionContext> _receivedContexts = [];

        public int ExecutionCount => _executionCount;

        public int MaximumObservedConcurrency => _maximumObservedConcurrency;

        public IReadOnlyCollection<ConnectorExecutionContext> ReceivedContexts =>
            _receivedContexts.ToArray();

        public async Task<ToolExecutionResult> ExecuteAsync(
            ValidatedToolCall validatedToolCall,
            ConnectorExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executionCount);
            var activeExecutions = Interlocked.Increment(ref _activeExecutions);
            UpdateMaximumConcurrency(activeExecutions);
            _receivedContexts.Enqueue(executionContext);

            try
            {
                return await execute(validatedToolCall, executionContext, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _activeExecutions);
            }
        }

        private void UpdateMaximumConcurrency(int activeExecutions)
        {
            int currentMaximum;
            do
            {
                currentMaximum = _maximumObservedConcurrency;
                if (activeExecutions <= currentMaximum)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(
                       ref _maximumObservedConcurrency,
                       activeExecutions,
                       currentMaximum) != currentMaximum);
        }
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
