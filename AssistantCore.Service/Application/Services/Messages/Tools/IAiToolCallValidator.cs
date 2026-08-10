using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public interface IAiToolCallValidator
{
    Task<ValidatedToolCall> ValidateAsync(
        AiRequestedToolCall requestedToolCall,
        IReadOnlyCollection<AiToolDefinition> availableTools,
        CancellationToken cancellationToken);
}
