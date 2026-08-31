using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Orchestration;

namespace AssistantCore.Service.Tests.Messages;

public sealed class OrchestrationContinuationPolicyTests
{
    [Theory, AutoDomainData]
    public void Given_TheModelReturnedAnAnswer_When_Evaluate_Then_StopsWithoutRequestingTools(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var state = CreateState(processing, now);
        var decision = CreateDecision(AiModelDecisionType.Answer, []);
        var policy = CreatePolicy(now);

        // When
        var result = policy.Evaluate(state, decision);

        // Then
        Assert.False(result.CanContinue);
        Assert.Equal(OrchestrationStopReason.ModelCompleted, result.StopReason);
    }

    [Theory, AutoDomainData]
    public void Given_TheLastToolRoundAddedNoEvidence_When_EvaluateWithADifferentSearch_Then_Continues(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var state = CreateState(processing, now);
        state.RecordToolResults([ToolExecutionResult.Succeeded("call-previous", [])]);
        var modelDecision = CreateDecision(
            AiModelDecisionType.UseTools,
            [CreateCall("call-next", "next query")]);
        var policy = CreatePolicy(now);

        // When
        var decision = policy.Evaluate(state, modelDecision);

        // Then
        Assert.True(decision.CanContinue);
        Assert.Null(decision.StopReason);
    }

    [Theory, AutoDomainData]
    public void Given_NoToolWasRequested_When_Evaluate_Then_Stops(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var state = CreateState(processing, now);
        var modelDecision = CreateDecision(AiModelDecisionType.UseTools, []);
        var policy = CreatePolicy(now);

        // When
        var decision = policy.Evaluate(state, modelDecision);

        // Then
        Assert.False(decision.CanContinue);
        Assert.Equal(OrchestrationStopReason.NoToolRequested, decision.StopReason);
    }

    [Theory, AutoDomainData]
    public void Given_ARequestedToolIsUnavailable_When_Evaluate_Then_Stops(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var state = CreateState(processing, now);
        var unavailableCall = CreateCall("call-next", "next query") with
        {
            ToolName = AiToolNames.QueryErp
        };
        var modelDecision = CreateDecision(
            AiModelDecisionType.UseTools,
            [unavailableCall]);
        var policy = CreatePolicy(now);

        // When
        var decision = policy.Evaluate(state, modelDecision);

        // Then
        Assert.False(decision.CanContinue);
        Assert.Equal(OrchestrationStopReason.ToolNotAllowed, decision.StopReason);
    }

    [Theory, AutoDomainData]
    public void Given_OneRepeatedSearchIsAllowed_When_Evaluate_Then_Continues(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var state = CreateState(processing, now);
        var previousCall = CreateCall("call-previous", "same query");
        var fingerprintGenerator = new ToolCallFingerprintGenerator();
        state.AcceptToolCalls(
            [previousCall],
            [fingerprintGenerator.CreateFingerprint(previousCall)],
            now);
        state.RecordToolResults([
            ToolExecutionResult.Succeeded(
                previousCall.CallId,
                [CreateEvidence("evidence-001")])
        ]);
        var repeatedCall = CreateCall("call-next", "same query");
        var modelDecision = CreateDecision(
            AiModelDecisionType.UseTools,
            [repeatedCall]);
        var policy = new OrchestrationContinuationPolicy(
            fingerprintGenerator,
            new StubTimeProvider(now));

        // When
        var decision = policy.Evaluate(state, modelDecision);

        // Then
        Assert.True(decision.CanContinue);
        Assert.Null(decision.StopReason);
    }

    [Theory, AutoDomainData]
    public void Given_TheToolCallBudgetIsExhausted_When_Evaluate_Then_Stops(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var state = CreateState(processing, now, maximumToolCalls: 0);
        var modelDecision = CreateDecision(
            AiModelDecisionType.UseTools,
            [CreateCall("call-next", "next query")]);
        var policy = CreatePolicy(now);

        // When
        var decision = policy.Evaluate(state, modelDecision);

        // Then
        Assert.False(decision.CanContinue);
        Assert.Equal(OrchestrationStopReason.BudgetExceeded, decision.StopReason);
        Assert.Equal(OrchestrationBudgetType.ToolCalls, decision.ExceededBudget);
    }

    [Theory, AutoDomainData]
    public void Given_NewEvidenceAndAUsefulAllowedSearch_When_Evaluate_Then_Continues(
        StartedMessageProcessing processing,
        DateTimeOffset now)
    {
        // Given
        var state = CreateState(processing, now);
        state.RecordToolResults([
            ToolExecutionResult.Succeeded(
                "call-previous",
                [CreateEvidence("evidence-001")])
        ]);
        var modelDecision = CreateDecision(
            AiModelDecisionType.UseTools,
            [CreateCall("call-next", "different query")]);
        var policy = CreatePolicy(now);

        // When
        var decision = policy.Evaluate(state, modelDecision);

        // Then
        Assert.True(decision.CanContinue);
        Assert.Null(decision.StopReason);
        Assert.Null(decision.ExceededBudget);
    }

    private static OrchestrationContinuationPolicy CreatePolicy(DateTimeOffset now) =>
        new(new ToolCallFingerprintGenerator(), new StubTimeProvider(now));

    private static MessageOrchestrationState CreateState(
        StartedMessageProcessing processing,
        DateTimeOffset now,
        int maximumToolCalls = 5)
    {
        var tool = new AiToolDefinition(
            AiToolNames.SearchInternalData,
            "Search internal data.",
            JsonSerializer.SerializeToElement(new { type = "object" }));
        var limits = new OrchestrationExecutionLimits(
            MaximumExecutionTime: TimeSpan.FromMinutes(2),
            MaximumToolCalls: maximumToolCalls,
            MaximumModelTokens: 12_000,
            MaximumEstimatedCost: 1.25m,
            MaximumResultsPerTool: 20,
            MaximumContextSize: 30_000,
            MaximumRepeatedToolCalls: 1,
            MaximumParallelToolCalls: 2);

        return MessageOrchestrationState.Start(
            processing,
            new("OpenAI", "gpt-5.6-luna"),
            [],
            [tool],
            limits,
            now);
    }

    private static AiModelDecision CreateDecision(
        AiModelDecisionType decisionType,
        IReadOnlyCollection<AiRequestedToolCall> requestedToolCalls) =>
        new(
            decisionType,
            "Reason",
            requestedToolCalls,
            decisionType == AiModelDecisionType.UseTools ? null : "Answer",
            []);

    private static AiRequestedToolCall CreateCall(string callId, string query) => new(
        callId,
        AiToolNames.SearchInternalData,
        JsonSerializer.SerializeToElement(new { query }));

    private static RetrievedEvidence CreateEvidence(string evidenceId) => new(
        evidenceId,
        "Internal",
        "Title",
        "Content",
        evidenceId,
        Url: null,
        OccurredAt: null);

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
