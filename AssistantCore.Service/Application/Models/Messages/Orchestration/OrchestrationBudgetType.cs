namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public enum OrchestrationBudgetType
{
    ExecutionTime,
    ToolCalls,
    ModelTokens,
    EstimatedCost,
    ContextSize,
    RepeatedToolCalls
}
