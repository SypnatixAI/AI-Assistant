using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public sealed class ToolExecutionRouter(
    IEnumerable<IAiToolExecutionHandler> toolHandlers) : IToolExecutionRouter
{
    public Task<ToolExecutionResult> ExecuteAsync(
        ValidatedToolCall toolCall,
        ConnectorExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolCall);
        ArgumentNullException.ThrowIfNull(executionContext);
        cancellationToken.ThrowIfCancellationRequested();

        var matchingHandlers = toolHandlers
            .Where(handler => string.Equals(
                handler.ToolName,
                toolCall.ToolName,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray();

        return matchingHandlers.Length switch
        {
            1 => matchingHandlers[0].ExecuteAsync(
                toolCall,
                executionContext,
                cancellationToken),
            0 => Task.FromResult(ToolExecutionResult.Failed(
                toolCall.CallId,
                ToolExecutionErrorCodes.ExecutorNotFound)),
            _ => Task.FromResult(ToolExecutionResult.Failed(
                toolCall.CallId,
                ToolExecutionErrorCodes.ExecutorAmbiguous))
        };
    }
}
