using AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;

namespace AssistantCore.Service.Application.Services.Messages.AiModels.Adapters.OpenAI;

public sealed record OpenAiResponsesResult(
    string OutputText,
    IReadOnlyCollection<OpenAiToolCall> ToolCalls,
    int InputTokens,
    int OutputTokens);
