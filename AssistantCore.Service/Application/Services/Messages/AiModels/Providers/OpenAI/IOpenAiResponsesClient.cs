using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.AiModels.Adapters.OpenAI;

namespace AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;

public interface IOpenAiResponsesClient
{
    Task<OpenAiResponsesResult> CreateResponseAsync(
        AiModelRequest request,
        CancellationToken cancellationToken);
}
