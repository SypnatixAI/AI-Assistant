using System.Text.Json;

namespace AssistantCore.ExternalServices.Entities.Foundry;

public enum FoundryAgentExternalMessageRole
{
    User,
    Assistant
}

public sealed record FoundryAgentExternalMessage(
    FoundryAgentExternalMessageRole Role,
    string Content);

public sealed record FoundryAgentExternalToolDefinition(
    string Name,
    string Description,
    JsonElement InputSchema);

public sealed record FoundryAgentExternalToolCall(
    string Name,
    JsonElement Arguments);

public sealed record FoundryAgentExternalRequest(
    IReadOnlyCollection<FoundryAgentExternalMessage> ConversationHistory,
    string UserMessage,
    IReadOnlyCollection<FoundryAgentExternalToolDefinition> Tools);

public sealed record FoundryAgentExternalResult(
    string Content,
    string AgentIdentifier,
    int InputTokens,
    int OutputTokens,
    int ModelCallCount);
