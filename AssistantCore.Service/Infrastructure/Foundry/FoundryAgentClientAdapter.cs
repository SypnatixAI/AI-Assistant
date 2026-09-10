using AssistantCore.ExternalServices.Entities.Foundry;
using AssistantCore.ExternalServices.Services.Foundry;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.AgentRuntime;

namespace AssistantCore.Service.Infrastructure.Foundry;

public sealed class FoundryAgentClientAdapter(
    FoundryAgentExternalClient externalClient) : IFoundryAgentClient
{
    public async Task<FoundryAgentClientResult> RunAsync(
        FoundryAgentClientRequest request,
        FoundryAgentToolExecutor toolExecutor,
        CancellationToken cancellationToken)
    {
        var result = await externalClient.RunAsync(
            MapRequest(request),
            (toolCall, token) => toolExecutor(
                new FoundryAgentToolCall(toolCall.Name, toolCall.Arguments),
                token),
            cancellationToken);

        return MapResult(result);
    }

    public async Task<FoundryAgentClientResult> RunStreamingAsync(
        FoundryAgentClientRequest request,
        FoundryAgentToolExecutor toolExecutor,
        Func<string, CancellationToken, ValueTask> onAnswerDelta,
        CancellationToken cancellationToken)
    {
        var result = await externalClient.RunStreamingAsync(
            MapRequest(request),
            (toolCall, token) => toolExecutor(
                new FoundryAgentToolCall(toolCall.Name, toolCall.Arguments),
                token),
            onAnswerDelta,
            cancellationToken);

        return MapResult(result);
    }

    private static FoundryAgentExternalRequest MapRequest(
        FoundryAgentClientRequest request) =>
        new(
            request.ConversationHistory.Select(MapMessage).ToArray(),
            request.UserMessage,
            request.Tools
                .Select(tool => new FoundryAgentExternalToolDefinition(
                    tool.Name,
                    tool.Description,
                    tool.InputSchema))
                .ToArray(),
            request.ConversationId);

    private static FoundryAgentExternalMessage MapMessage(AiConversationMessage message) =>
        new(
            message.Role == AiConversationRole.Assistant
                ? FoundryAgentExternalMessageRole.Assistant
                : FoundryAgentExternalMessageRole.User,
            message.Content);

    private static FoundryAgentClientResult MapResult(
        FoundryAgentExternalResult result) =>
        new(
            result.Content,
            result.AgentIdentifier,
            result.InputTokens,
            result.OutputTokens,
            result.ModelCallCount);
}
