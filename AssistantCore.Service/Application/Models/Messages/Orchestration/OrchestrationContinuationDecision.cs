namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed record OrchestrationContinuationDecision(
    bool CanContinue,
    OrchestrationStopReason? StopReason,
    OrchestrationBudgetType? ExceededBudget = null);
