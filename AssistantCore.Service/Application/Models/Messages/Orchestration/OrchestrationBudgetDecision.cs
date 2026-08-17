namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed record OrchestrationBudgetDecision(
    OrchestrationBudgetType? ExceededBudget)
{
    public bool IsAllowed => ExceededBudget is null;

    public static OrchestrationBudgetDecision Allowed { get; } =
        new(ExceededBudget: null);
}
