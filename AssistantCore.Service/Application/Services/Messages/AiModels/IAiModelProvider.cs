using AssistantCore.Service.Application.Models.Messages.AiModels;

namespace AssistantCore.Service.Application.Services.Messages.AiModels;

public interface IAiModelProvider
{
    string ModelFamily { get; }

    Task<AiModelResponse> GetNextActionAsync(
        AiModelRequest request,
        CancellationToken cancellationToken);
}
