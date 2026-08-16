using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public sealed class AiToolExecutor(
    IEnumerable<IAiToolExecutionHandler> executionHandlers) : IAiToolExecutor
{
    public Task<ToolExecutionResult> ExecuteAsync(
        ValidatedToolCall validatedToolCall,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var matchingHandlers = executionHandlers
            .Where(handler => string.Equals(
                handler.ToolName,
                validatedToolCall.ToolName,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray();

        return matchingHandlers.Length switch
        {
            1 => matchingHandlers[0].ExecuteAsync(validatedToolCall, cancellationToken),
            0 => Task.FromResult(ToolExecutionResult.Failed(
                validatedToolCall.CallId,
                ToolExecutionErrorCodes.ExecutorNotFound)),
            _ => Task.FromResult(ToolExecutionResult.Failed(
                validatedToolCall.CallId,
                ToolExecutionErrorCodes.ExecutorAmbiguous))
        };
    }
}
