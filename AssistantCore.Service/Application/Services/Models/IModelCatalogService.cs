using AssistantCore.Service.Application.Models.Models;

namespace AssistantCore.Service.Application.Services.Models;

public interface IModelCatalogService
{
    Task<GetAvailableModelsResponse> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
}
