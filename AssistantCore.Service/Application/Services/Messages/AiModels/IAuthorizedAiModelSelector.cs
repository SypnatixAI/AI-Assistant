using AssistantCore.Service.Application.Models.Messages.AiModels;

namespace AssistantCore.Service.Application.Services.Messages.AiModels;

public interface IAuthorizedAiModelSelector
{
    Task<SelectedAiModel> SelectAsync(
        Guid organizationId,
        string? requestedModel,
        CancellationToken cancellationToken);
}
