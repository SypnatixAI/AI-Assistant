using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Messages;

public sealed class MessageToolOrchestratorTests
{
    [Theory, AutoDomainData]
    public async Task Given_AToolRoundFollowedByAnAnswer_When_OrchestrateAsync_Then_ExecutesToolsBeforeBuildingResult(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        MessageOrchestrationResult expectedResult,
        DateTimeOffset now)
    {
        // Given
        var operations = new List<string>();
        var responses = new Queue<AiModelResponse>(
        [
            CreateResponse(AiModelDecisionType.UseTools),
            CreateResponse(AiModelDecisionType.Answer)
        ]);
        var orchestrator = new MessageToolOrchestrator(
            new StubModelTurnService(operations, responses),
            new StubContinuationPolicy(),
            new StubToolCallBatchExecutor(operations),
            new StubResultBuilder(operations, expectedResult),
            Options.Create(CreateOptions()),
            new StubTimeProvider(now));

        // When
        var result = await orchestrator.OrchestrateAsync(
            processing,
            selectedModel,
            [],
            [],
            CancellationToken.None);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(
            ["ModelTurn", "ExecuteTools", "ModelTurn", "BuildResult"],
            operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnExceededBudget_When_OrchestrateAsync_Then_RequestsAFinalResponse(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        MessageOrchestrationResult expectedResult,
        DateTimeOffset now)
    {
        // Given
        var operations = new List<string>();
        var responses = new Queue<AiModelResponse>(
        [
            CreateResponse(AiModelDecisionType.UseTools),
            CreateResponse(AiModelDecisionType.Answer)
        ]);
        var orchestrator = new MessageToolOrchestrator(
            new StubModelTurnService(operations, responses),
            new StubBudgetExceededPolicy(),
            new StubToolCallBatchExecutor(operations),
            new StubResultBuilder(operations, expectedResult),
            Options.Create(CreateOptions()),
            new StubTimeProvider(now));

        // When
        var result = await orchestrator.OrchestrateAsync(
            processing,
            selectedModel,
            [],
            [],
            CancellationToken.None);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(["ModelTurn", "ModelTurn", "BuildResult"], operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_AFinalStreamingAnswer_When_OrchestrateStreamingAsync_Then_ForwardsAnswerDeltasAndBuildsResult(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext,
        SelectedAiModel selectedModel,
        MessageOrchestrationResult generatedResult,
        DateTimeOffset now)
    {
        // Given
        var expectedResult = generatedResult with { Answer = "Bonjour monde" };
        var operations = new List<string>();
        var receivedDeltas = new List<string>();
        var modelTurnService = new StubModelTurnService(
            operations,
            new Queue<AiModelResponse>([CreateResponse(AiModelDecisionType.Answer)]),
            ["Bonjour", " monde"]);
        var orchestrator = new MessageToolOrchestrator(
            modelTurnService,
            new StubContinuationPolicy(),
            new StubToolCallBatchExecutor(operations),
            new StubResultBuilder(operations, expectedResult),
            Options.Create(CreateOptions()),
            new StubTimeProvider(now));

        // When
        var result = await orchestrator.OrchestrateStreamingAsync(
            processing,
            executionContext,
            selectedModel,
            [],
            [],
            (_, _) => ValueTask.CompletedTask,
            (delta, _) =>
            {
                receivedDeltas.Add(delta);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(["Bonjour", " monde"], receivedDeltas);
        Assert.Equal(["StreamingModelTurn", "BuildResult"], operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnsafeFinalStreamingAnswer_When_OrchestrateStreamingAsync_Then_ForwardsTheSanitizedBuiltAnswer(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext,
        SelectedAiModel selectedModel,
        MessageOrchestrationResult generatedResult,
        DateTimeOffset now)
    {
        // Given
        var expectedResult = generatedResult with { Answer = "Réponse finale." };
        var operations = new List<string>();
        var receivedDeltas = new List<string>();
        var modelTurnService = new StubModelTurnService(
            operations,
            new Queue<AiModelResponse>([CreateResponse(AiModelDecisionType.Answer)]),
            ["Réponse finale. ", "[evidence-757496c563c6593a56b787fd]"]);
        var orchestrator = new MessageToolOrchestrator(
            modelTurnService,
            new StubContinuationPolicy(),
            new StubToolCallBatchExecutor(operations),
            new StubResultBuilder(operations, expectedResult),
            Options.Create(CreateOptions()),
            new StubTimeProvider(now));

        // When
        var result = await orchestrator.OrchestrateStreamingAsync(
            processing,
            executionContext,
            selectedModel,
            [],
            [],
            (_, _) => ValueTask.CompletedTask,
            (delta, _) =>
            {
                receivedDeltas.Add(delta);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(["Réponse finale."], receivedDeltas);
        Assert.Equal(["StreamingModelTurn", "BuildResult"], operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnIntermediateToolTurn_When_OrchestrateStreamingAsync_Then_ReportsProgressWithoutForwardingItsAnswer(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext,
        SelectedAiModel selectedModel,
        MessageOrchestrationResult generatedResult,
        DateTimeOffset now)
    {
        // Given
        var expectedResult = generatedResult with { Answer = "Réponse finale" };
        var operations = new List<string>();
        var receivedProgress = new List<string>();
        var receivedDeltas = new List<string>();
        var responses = new Queue<AiModelResponse>(
        [
            CreateResponse(AiModelDecisionType.UseTools, "Je consulte les documents pertinents."),
            CreateResponse(AiModelDecisionType.Answer, "J’ai trouvé une source utile.")
        ]);
        var streamingDeltas = new Queue<IReadOnlyCollection<string>>(
        [
            ["Brouillon intermédiaire"],
            ["Réponse ", "finale"]
        ]);
        var orchestrator = new MessageToolOrchestrator(
            new StubModelTurnService(
                operations,
                responses,
                streamingDeltasByTurn: streamingDeltas),
            new StubContinuationPolicy(),
            new StubToolCallBatchExecutor(operations),
            new StubResultBuilder(operations, expectedResult),
            Options.Create(CreateOptions()),
            new StubTimeProvider(now));

        // When
        var result = await orchestrator.OrchestrateStreamingAsync(
            processing,
            executionContext,
            selectedModel,
            [],
            [],
            (message, _) =>
            {
                receivedProgress.Add(message);
                return ValueTask.CompletedTask;
            },
            (delta, _) =>
            {
                receivedDeltas.Add(delta);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(
            ["Je consulte les documents pertinents.", "J’ai trouvé une source utile."],
            receivedProgress);
        Assert.Equal(["Réponse ", "finale"], receivedDeltas);
        Assert.Equal(
            ["StreamingModelTurn", "ExecuteTools", "StreamingModelTurn", "BuildResult"],
            operations);
    }

    private static MessageOrchestrationOptions CreateOptions() =>
        new()
        {
            MaximumExecutionTimeSeconds = 120,
            MaximumToolCalls = 8,
            MaximumModelTokens = 12_000,
            MaximumEstimatedCost = 1.25m,
            MaximumResultsPerTool = 20,
            MaximumContextSize = 30_000,
            MaximumRepeatedToolCalls = 2,
            MaximumParallelToolCalls = 4
        };

    private static AiModelResponse CreateResponse(
        AiModelDecisionType decisionType,
        string? progressMessage = null) =>
        new(
            new AiModelDecision(
                decisionType,
                "Reason",
                decisionType == AiModelDecisionType.UseTools
                    ? [new AiRequestedToolCall("call-1", "tool", default)]
                    : [],
                decisionType == AiModelDecisionType.Answer ? "Answer" : null,
                [],
                progressMessage),
            new AiModelUsage(1, 1, 1, 0, 0.01m));

    private sealed class StubModelTurnService(
        List<string> operations,
        Queue<AiModelResponse> responses,
        IReadOnlyCollection<string>? streamingDeltas = null,
        Queue<IReadOnlyCollection<string>>? streamingDeltasByTurn = null) : IAiModelTurnService
    {
        public Task<AiModelResponse> RequestNextActionAsync(
            MessageOrchestrationState state,
            CancellationToken cancellationToken)
        {
            operations.Add("ModelTurn");
            return Task.FromResult(responses.Dequeue());
        }

        public Task<AiModelResponse> RequestNextActionStreamingAsync(
            MessageOrchestrationState state,
            Func<string, CancellationToken, ValueTask> onAnswerDelta,
            CancellationToken cancellationToken) =>
            StreamAsync(state, onAnswerDelta, cancellationToken);

        private async Task<AiModelResponse> StreamAsync(
            MessageOrchestrationState state,
            Func<string, CancellationToken, ValueTask> onAnswerDelta,
            CancellationToken cancellationToken)
        {
            operations.Add("StreamingModelTurn");
            var currentDeltas = streamingDeltasByTurn?.Dequeue() ?? streamingDeltas ?? [];
            foreach (var delta in currentDeltas)
            {
                await onAnswerDelta(delta, cancellationToken);
            }

            return responses.Dequeue();
        }
    }

    private sealed class StubContinuationPolicy : IOrchestrationContinuationPolicy
    {
        public OrchestrationContinuationDecision Evaluate(
            MessageOrchestrationState state,
            AiModelDecision decision) =>
            decision.Type == AiModelDecisionType.UseTools
                ? new(true, null)
                : new(false, OrchestrationStopReason.ModelCompleted);
    }

    private sealed class StubBudgetExceededPolicy : IOrchestrationContinuationPolicy
    {
        public OrchestrationContinuationDecision Evaluate(
            MessageOrchestrationState state,
            AiModelDecision decision) =>
            decision.Type == AiModelDecisionType.UseTools
                ? new(
                    CanContinue: false,
                    OrchestrationStopReason.BudgetExceeded,
                    OrchestrationBudgetType.ToolCalls)
                : new(CanContinue: false, OrchestrationStopReason.ModelCompleted);
    }

    private sealed class StubToolCallBatchExecutor(List<string> operations)
        : IToolCallBatchExecutor
    {
        public Task<IReadOnlyCollection<ToolExecutionResult>> ExecuteAsync(
            MessageOrchestrationState state,
            IReadOnlyCollection<AiRequestedToolCall> requestedToolCalls,
            CancellationToken cancellationToken)
        {
            operations.Add("ExecuteTools");
            return Task.FromResult<IReadOnlyCollection<ToolExecutionResult>>([]);
        }
    }

    private sealed class StubResultBuilder(
        List<string> operations,
        MessageOrchestrationResult? result) : IOrchestrationResultBuilder
    {
        public MessageOrchestrationResult Build(
            MessageOrchestrationState state,
            AiModelResponse finalResponse)
        {
            operations.Add("BuildResult");
            return result ?? throw new InvalidOperationException("No result configured.");
        }
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
