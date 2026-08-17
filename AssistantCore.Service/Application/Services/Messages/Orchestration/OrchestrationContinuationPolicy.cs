using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public sealed class OrchestrationContinuationPolicy(
    IToolCallFingerprintGenerator fingerprintGenerator,
    TimeProvider timeProvider) : IOrchestrationContinuationPolicy
{
    public OrchestrationContinuationDecision Evaluate(
        MessageOrchestrationState state,
        AiModelDecision decision)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.Type != AiModelDecisionType.UseTools)
        {
            return Stop(OrchestrationStopReason.ModelCompleted);
        }

        if (state.HasExecutedToolCalls
            && !state.LastToolRoundAddedEvidence)
        {
            return Stop(OrchestrationStopReason.NoNewEvidence);
        }

        var requestedToolCalls = decision.ToolCalls;
        if (requestedToolCalls.Count == 0)
        {
            return Stop(OrchestrationStopReason.NoToolRequested);
        }

        var allowedToolNames = state.AllowedTools
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (requestedToolCalls.Any(call => !allowedToolNames.Contains(call.ToolName)))
        {
            return Stop(OrchestrationStopReason.ToolNotAllowed);
        }

        var fingerprints = requestedToolCalls
            .Select(fingerprintGenerator.CreateFingerprint)
            .ToArray();
        var budgetDecision = state.Budget.EvaluateToolCalls(
            requestedToolCalls.Count,
            fingerprints,
            timeProvider.GetUtcNow());
        if (!budgetDecision.IsAllowed)
        {
            return new OrchestrationContinuationDecision(
                CanContinue: false,
                OrchestrationStopReason.BudgetExceeded,
                budgetDecision.ExceededBudget);
        }

        return new OrchestrationContinuationDecision(
            CanContinue: true,
            StopReason: null);
    }

    private static OrchestrationContinuationDecision Stop(
        OrchestrationStopReason reason) =>
        new(CanContinue: false, reason);
}
