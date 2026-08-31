using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.AiModels.Adapters.OpenAI;

namespace AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;

public interface IOpenAiResponsesClient
{
    Task<OpenAiResponsesResult> CreateResponseAsync(
        AiModelRequest request,
        CancellationToken cancellationToken);

    Task<OpenAiResponsesResult> CreateResponseStreamingAsync(
        AiModelRequest request,
        Func<string, CancellationToken, ValueTask> onTextDelta,
        CancellationToken cancellationToken);

    Task<string> CreateConversationSummaryAsync(
        AiConversationSummaryRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Conversation summarization is not supported by this client.");
}
