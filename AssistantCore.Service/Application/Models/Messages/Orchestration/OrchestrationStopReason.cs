namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public enum OrchestrationStopReason
{
    ModelCompleted,
    NoNewEvidence,
    NoToolRequested,
    ToolNotAllowed,
    BudgetExceeded
}
