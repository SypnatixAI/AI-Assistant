using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public interface IAiToolExecutionHandler
{
    string ToolName { get; }

    Task<ToolExecutionResult> ExecuteAsync(
        ValidatedToolCall validatedToolCall,
        CancellationToken cancellationToken);
}
