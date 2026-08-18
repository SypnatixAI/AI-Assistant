using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public abstract class AiToolExecutionHandler<TArguments>(string toolName)
    : IAiToolExecutionHandler
    where TArguments : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web);

    public string ToolName { get; } = toolName;

    public async Task<ToolExecutionResult> ExecuteAsync(
        ValidatedToolCall validatedToolCall,
        ConnectorExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validatedToolCall);
        ArgumentNullException.ThrowIfNull(executionContext);
        cancellationToken.ThrowIfCancellationRequested();

        TArguments? arguments;
        try
        {
            arguments = validatedToolCall.Arguments.Deserialize<TArguments>(SerializerOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return ToolExecutionResult.Failed(
                validatedToolCall.CallId,
                ToolExecutionErrorCodes.ArgumentMappingFailed);
        }

        if (arguments is null)
        {
            return ToolExecutionResult.Failed(
                validatedToolCall.CallId,
                ToolExecutionErrorCodes.ArgumentMappingFailed);
        }

        return await ExecuteAsync(
            validatedToolCall.CallId,
            arguments,
            executionContext,
            cancellationToken);
    }

    protected abstract Task<ToolExecutionResult> ExecuteAsync(
        string toolCallId,
        TArguments arguments,
        ConnectorExecutionContext executionContext,
        CancellationToken cancellationToken);
}
