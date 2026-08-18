using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Exceptions;

public sealed class OrchestrationBudgetExceededException(
    OrchestrationBudgetType exceededBudget)
    : Exception($"The orchestration budget '{exceededBudget}' has been reached.")
{
    public OrchestrationBudgetType ExceededBudget { get; } = exceededBudget;
}
