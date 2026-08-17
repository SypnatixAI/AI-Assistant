using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public interface IToolCallBatchExecutor
{
    Task<IReadOnlyCollection<ToolExecutionResult>> ExecuteAsync(
        MessageOrchestrationState state,
        IReadOnlyCollection<AiRequestedToolCall> requestedToolCalls,
        CancellationToken cancellationToken);
}
