using AssistantCore.Service.Application.Models.Messages.AiModels;

namespace AssistantCore.Service.Application.Services.Messages.AiModels;

public interface IAiModelProvider
{
    string ProviderName { get; }

    Task<AiModelResponse> GetNextActionAsync(
        AiModelRequest request,
        CancellationToken cancellationToken);

    Task<AiModelResponse> GetNextActionStreamingAsync(
        AiModelRequest request,
        Func<string, CancellationToken, ValueTask> onTextDelta,
        CancellationToken cancellationToken);

    Task<string> CreateConversationSummaryAsync(
        AiConversationSummaryRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Conversation summarization is not supported by this provider.");
}
