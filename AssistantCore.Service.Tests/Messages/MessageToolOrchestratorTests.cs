using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Rag;
using Microsoft.Extensions.Logging.Abstractions;
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
        var modelTurnService = new StubModelTurnService(operations, responses);
        var orchestrator = new MessageToolOrchestrator(
            modelTurnService,
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
        var finalTurnToolResult = Assert.Single(modelTurnService.ModelVisibleToolResultsByTurn[1]);
        Assert.Equal("call-1", finalTurnToolResult.ToolCallId);
        Assert.Equal(ToolExecutionStatus.Failed, finalTurnToolResult.Status);
        Assert.Equal("TOOL_BUDGET_EXCEEDED", finalTurnToolResult.ErrorCode);
        Assert.Empty(finalTurnToolResult.Warnings);
    }

    [Theory, AutoDomainData]
    public async Task Given_FinalAnswerWithUnknownCitation_When_OrchestrateAsync_Then_RequestsCitationRepairResponse(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        DateTimeOffset now)
    {
        // Given
        var operations = new List<string>();
        var modelTurnService = new StubModelTurnService(
            operations,
            new Queue<AiModelResponse>(
            [
                CreateResponse(
                    AiModelDecisionType.Answer,
                    citedEvidenceIds: ["unknown-evidence"]),
                CreateResponse(
                    AiModelDecisionType.Answer,
                    answer: "Corrected answer.")
            ]));
        var orchestrator = new MessageToolOrchestrator(
            modelTurnService,
            new StubContinuationPolicy(),
            new StubToolCallBatchExecutor(operations),
            new RecordingOrchestrationResultBuilder(operations),
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
        Assert.Equal("Corrected answer.", result.Answer);
        Assert.Equal(
            ["ModelTurn", "BuildResult", "ModelTurn", "BuildResult"],
            operations);
        Assert.Equal(
            [false, true],
            modelTurnService.CitationRepairResponseRequiredValues);
    }

    [Theory, AutoDomainData]
    public async Task Given_RepairedAnswerStillHasUnknownCitation_When_OrchestrateAsync_Then_RejectsTheProviderResponse(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        DateTimeOffset now)
    {
        // Given
        var operations = new List<string>();
        var orchestrator = new MessageToolOrchestrator(
            new StubModelTurnService(
                operations,
                new Queue<AiModelResponse>(
                [
                    CreateResponse(
                        AiModelDecisionType.Answer,
                        citedEvidenceIds: ["first-unknown-evidence"]),
                    CreateResponse(
                        AiModelDecisionType.Answer,
                        citedEvidenceIds: ["second-unknown-evidence"])
                ])),
            new StubContinuationPolicy(),
            new StubToolCallBatchExecutor(operations),
            new RecordingOrchestrationResultBuilder(operations),
            Options.Create(CreateOptions()),
            new StubTimeProvider(now));

        // When
        var exception = await Record.ExceptionAsync(() =>
            orchestrator.OrchestrateAsync(
                processing,
                selectedModel,
                [],
                [],
                CancellationToken.None));

        // Then
        Assert.IsType<AiProviderInvalidCitationResponseException>(exception);
        Assert.Equal(
            ["ModelTurn", "BuildResult", "ModelTurn", "BuildResult"],
            operations);
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

    [Theory, AutoDomainData]
    public async Task Given_AnExceededBudget_When_OrchestrateStreamingAsync_Then_RecordsFailedToolResultsBeforeFinalResponse(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext,
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
        var modelTurnService = new StubModelTurnService(operations, responses);
        var orchestrator = new MessageToolOrchestrator(
            modelTurnService,
            new StubBudgetExceededPolicy(),
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
            (_, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(["StreamingModelTurn", "StreamingModelTurn", "BuildResult"], operations);
        var finalTurnToolResult = Assert.Single(modelTurnService.ModelVisibleToolResultsByTurn[1]);
        Assert.Equal("call-1", finalTurnToolResult.ToolCallId);
        Assert.Equal(ToolExecutionStatus.Failed, finalTurnToolResult.Status);
        Assert.Equal("TOOL_BUDGET_EXCEEDED", finalTurnToolResult.ErrorCode);
        Assert.Empty(finalTurnToolResult.Warnings);
    }

    [Theory, AutoDomainData]
    public async Task Given_ExhaustedTokenBudgetAndThreeRejectedAnswers_When_OrchestrateAsync_Then_ReturnsTheThirdReformulation(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        MessageOrchestrationResult expectedResult,
        DateTimeOffset now)
    {
        // Given
        var operations = new List<string>();
        var modelTurnService = new StubModelTurnService(
            operations,
            new Queue<AiModelResponse>(
            [
                CreateResponse(AiModelDecisionType.Answer),
                CreateResponse(AiModelDecisionType.Answer),
                CreateResponse(AiModelDecisionType.Answer),
                CreateResponse(AiModelDecisionType.Answer)
            ]));
        var groundednessGuard = new StubGroundednessGuard(
            operations,
            new Queue<bool>([false, false, false, true]),
            maximumReformulationAttempts: 3);
        var orchestrator = new MessageToolOrchestrator(
            modelTurnService,
            new StubContinuationPolicy(),
            new StubToolCallBatchExecutor(operations),
            new StubResultBuilder(operations, expectedResult),
            Options.Create(CreateOptions(maximumModelTokens: 1)),
            new StubTimeProvider(now),
            groundednessGuard);

        // When
        var result = await orchestrator.OrchestrateAsync(
            processing,
            selectedModel,
            [],
            [],
            CancellationToken.None);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(4, operations.Count(operation => operation == "ModelTurn"));
        Assert.Equal(4, operations.Count(operation => operation == "ValidateGroundedness"));
        Assert.Equal([false, true, true, true], modelTurnService.GroundednessReformulationRequiredValues);
    }

    [Theory, AutoDomainData]
    public async Task Given_AllGroundednessReformulationsAreRejected_When_OrchestrateAsync_Then_ReturnsInsufficientEvidenceAfterThreeAttempts(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        MessageOrchestrationResult generatedResult,
        DateTimeOffset now)
    {
        // Given
        var operations = new List<string>();
        var orchestrator = new MessageToolOrchestrator(
            new StubModelTurnService(
                operations,
                new Queue<AiModelResponse>(
                [
                    CreateResponse(AiModelDecisionType.Answer),
                    CreateResponse(AiModelDecisionType.Answer),
                    CreateResponse(AiModelDecisionType.Answer),
                    CreateResponse(AiModelDecisionType.Answer)
                ])),
            new StubContinuationPolicy(),
            new StubToolCallBatchExecutor(operations),
            new StubResultBuilder(operations, generatedResult),
            Options.Create(CreateOptions()),
            new StubTimeProvider(now),
            new StubGroundednessGuard(
                operations,
                new Queue<bool>([false, false, false, false]),
                maximumReformulationAttempts: 3));

        // When
        var result = await orchestrator.OrchestrateAsync(
            processing,
            selectedModel,
            [],
            [],
            CancellationToken.None);

        // Then
        Assert.Equal(AnswerGroundednessGuard.InsufficientEvidenceAnswer, result.Answer);
        Assert.Contains(IAnswerGroundednessGuard.ContentRejectedWarning, result.Warnings);
        Assert.Equal(4, operations.Count(operation => operation == "ModelTurn"));
        Assert.Equal(4, operations.Count(operation => operation == "ValidateGroundedness"));
    }

    [Theory, AutoDomainData]
    public async Task Given_ARejectedStreamingAnswer_When_OrchestrateStreamingAsync_Then_StreamsOnlyTheGroundedReformulation(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext,
        SelectedAiModel selectedModel,
        MessageOrchestrationResult generatedResult,
        DateTimeOffset now)
    {
        // Given
        var operations = new List<string>();
        var receivedDeltas = new List<string>();
        var expectedResult = generatedResult with { Answer = "Le projet Atlas se termine le 1er septembre." };
        var orchestrator = new MessageToolOrchestrator(
            new StubModelTurnService(
                operations,
                new Queue<AiModelResponse>(
                [
                    CreateResponse(AiModelDecisionType.Answer),
                    CreateResponse(AiModelDecisionType.Answer)
                ]),
                streamingDeltasByTurn: new Queue<IReadOnlyCollection<string>>(
                [
                    ["Le projet Atlas se termine le 1er septembre 2026."],
                    ["Le projet Atlas se termine le 1er septembre."]
                ])),
            new StubContinuationPolicy(),
            new StubToolCallBatchExecutor(operations),
            new StubResultBuilder(operations, expectedResult),
            Options.Create(CreateOptions()),
            new StubTimeProvider(now),
            new StubGroundednessGuard(
                operations,
                new Queue<bool>([false, true]),
                maximumReformulationAttempts: 3));

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
        Assert.Equal(["Le projet Atlas se termine le 1er septembre."], receivedDeltas);
        Assert.Equal(2, operations.Count(operation => operation == "StreamingModelTurn"));
    }

    private static MessageOrchestrationOptions CreateOptions(
        int maximumModelTokens = 12_000) =>
        new()
        {
            MaximumExecutionTimeSeconds = 120,
            MaximumToolCalls = 8,
            MaximumModelTokens = maximumModelTokens,
            MaximumEstimatedCost = 1.25m,
            RetrievalCandidateLimit = 20,
            FinalEvidenceLimit = 8,
            MaximumContextSize = 30_000,
            MaximumRepeatedToolCalls = 2,
            MaximumParallelToolCalls = 4
        };

    private static AiModelResponse CreateResponse(
        AiModelDecisionType decisionType,
        string? progressMessage = null,
        string? answer = null,
        IReadOnlyCollection<string>? citedEvidenceIds = null) =>
        new(
            new AiModelDecision(
                decisionType,
                "Reason",
                decisionType == AiModelDecisionType.UseTools
                    ? [new AiRequestedToolCall("call-1", "tool", default)]
                    : [],
                decisionType == AiModelDecisionType.Answer ? answer ?? "Answer" : null,
                citedEvidenceIds ?? [],
                progressMessage),
            new AiModelUsage(1, 1, 1, 0, 0.01m));

    private sealed class StubModelTurnService(
        List<string> operations,
        Queue<AiModelResponse> responses,
        IReadOnlyCollection<string>? streamingDeltas = null,
        Queue<IReadOnlyCollection<string>>? streamingDeltasByTurn = null) : IAiModelTurnService
    {
        public List<bool> CitationRepairResponseRequiredValues { get; } = [];

        public List<bool> GroundednessReformulationRequiredValues { get; } = [];

        public List<IReadOnlyCollection<ToolExecutionResult>> ModelVisibleToolResultsByTurn { get; } = [];

        public Task<AiModelResponse> RequestNextActionAsync(
            MessageOrchestrationState state,
            CancellationToken cancellationToken)
        {
            CitationRepairResponseRequiredValues.Add(state.CitationRepairResponseRequired);
            GroundednessReformulationRequiredValues.Add(state.GroundednessReformulationRequired);
            ModelVisibleToolResultsByTurn.Add(state.ModelVisibleToolResults);
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
            CitationRepairResponseRequiredValues.Add(state.CitationRepairResponseRequired);
            GroundednessReformulationRequiredValues.Add(state.GroundednessReformulationRequired);
            ModelVisibleToolResultsByTurn.Add(state.ModelVisibleToolResults);
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

    private sealed class RecordingOrchestrationResultBuilder(List<string> operations)
        : IOrchestrationResultBuilder
    {
        private readonly OrchestrationResultBuilder _inner = new(
            new EvidenceCitationResolver(),
            NullLogger<OrchestrationResultBuilder>.Instance);

        public MessageOrchestrationResult Build(
            MessageOrchestrationState state,
            AiModelResponse finalResponse)
        {
            operations.Add("BuildResult");
            return _inner.Build(state, finalResponse);
        }
    }

    private sealed class StubGroundednessGuard(
        List<string> operations,
        Queue<bool> validationResults,
        int maximumReformulationAttempts) : IAnswerGroundednessGuard
    {
        public int MaximumReformulationAttempts => maximumReformulationAttempts;

        public Task<MessageOrchestrationResult> ValidateAsync(
            MessageOrchestrationState state,
            MessageOrchestrationResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            operations.Add("ValidateGroundedness");
            if (validationResults.Dequeue())
            {
                return Task.FromResult(result);
            }

            return Task.FromResult(result with
            {
                Answer = AnswerGroundednessGuard.InsufficientEvidenceAnswer,
                CitedEvidence = [],
                Warnings = result.Warnings
                    .Append(IAnswerGroundednessGuard.ContentRejectedWarning)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            });
        }
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
