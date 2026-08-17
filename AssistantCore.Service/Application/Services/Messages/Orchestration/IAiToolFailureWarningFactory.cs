namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public interface IAiToolFailureWarningFactory
{
    string Create(string toolName);
}
