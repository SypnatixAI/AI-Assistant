using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;
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
    public async Task Given_AnExceededBudget_When_OrchestrateAsync_Then_ThrowsBudgetException(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        DateTimeOffset now)
    {
        // Given
        var operations = new List<string>();
        var responses = new Queue<AiModelResponse>(
            [CreateResponse(AiModelDecisionType.UseTools)]);
        var orchestrator = new MessageToolOrchestrator(
            new StubModelTurnService(operations, responses),
            new StubBudgetExceededPolicy(),
            new StubToolCallBatchExecutor(operations),
            new StubResultBuilder(operations, null),
            Options.Create(CreateOptions()),
            new StubTimeProvider(now));

        // When
        var exception = await Assert.ThrowsAsync<OrchestrationBudgetExceededException>(() =>
            orchestrator.OrchestrateAsync(
                processing,
                selectedModel,
                [],
                [],
                CancellationToken.None));

        // Then
        Assert.Equal(OrchestrationBudgetType.ToolCalls, exception.ExceededBudget);
        Assert.Equal(["ModelTurn"], operations);
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

    private static AiModelResponse CreateResponse(AiModelDecisionType decisionType) =>
        new(
            new AiModelDecision(
                decisionType,
                "Reason",
                decisionType == AiModelDecisionType.UseTools
                    ? [new AiRequestedToolCall("call-1", "tool", default)]
                    : [],
                decisionType == AiModelDecisionType.Answer ? "Answer" : null,
                []),
            new AiModelUsage(1, 1, 1, 0, 0.01m));

    private sealed class StubModelTurnService(
        List<string> operations,
        Queue<AiModelResponse> responses) : IAiModelTurnService
    {
        public Task<AiModelResponse> RequestNextActionAsync(
            MessageOrchestrationState state,
            CancellationToken cancellationToken)
        {
            operations.Add("ModelTurn");
            return Task.FromResult(responses.Dequeue());
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
            new(
                CanContinue: false,
                OrchestrationStopReason.BudgetExceeded,
                OrchestrationBudgetType.ToolCalls);
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
