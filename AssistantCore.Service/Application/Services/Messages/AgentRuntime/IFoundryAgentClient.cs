using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages.AiModels;

namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

public interface IFoundryAgentClient
{
    Task<FoundryAgentClientResult> RunAsync(
        FoundryAgentClientRequest request,
        FoundryAgentToolExecutor toolExecutor,
        CancellationToken cancellationToken);

    Task<FoundryAgentClientResult> RunStreamingAsync(
        FoundryAgentClientRequest request,
        FoundryAgentToolExecutor toolExecutor,
        Func<string, CancellationToken, ValueTask> onAnswerDelta,
        CancellationToken cancellationToken);
}

public delegate Task<string> FoundryAgentToolExecutor(
    FoundryAgentToolCall toolCall,
    CancellationToken cancellationToken);

public sealed record FoundryAgentClientRequest(
    IReadOnlyCollection<AiConversationMessage> ConversationHistory,
    string UserMessage,
    IReadOnlyCollection<FoundryAgentToolDefinition> Tools,
    // Stable application conversation identity used to reuse its Foundry session.
    Guid ConversationId = default);

public sealed record FoundryAgentToolDefinition(
    string Name,
    string Description,
    JsonElement InputSchema);

public sealed record FoundryAgentToolCall(
    string Name,
    JsonElement Arguments);

public sealed record FoundryAgentClientResult(
    string Content,
    string AgentIdentifier,
    int InputTokens,
    int OutputTokens,
    int ModelCallCount);
