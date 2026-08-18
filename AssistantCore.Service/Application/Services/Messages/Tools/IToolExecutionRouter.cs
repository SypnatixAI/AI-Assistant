using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public interface IToolExecutionRouter
{
    Task<ToolExecutionResult> ExecuteAsync(
        ValidatedToolCall toolCall,
        ConnectorExecutionContext executionContext,
        CancellationToken cancellationToken);
}
