namespace AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;

public sealed record OpenAiToolCall(
    string CallId,
    string Name,
    string ArgumentsJson);
