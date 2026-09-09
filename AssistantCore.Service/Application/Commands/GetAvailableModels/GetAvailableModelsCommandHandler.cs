using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Models;
using AssistantCore.Service.Application.Services.Models;

namespace AssistantCore.Service.Application.Commands.GetAvailableModels;

public sealed class GetAvailableModelsCommandHandler(
    IModelCatalogService modelCatalogService)
    : IRequestHandler<GetAvailableModelsCommand, GetAvailableModelsResponse>
{
    public Task<GetAvailableModelsResponse> HandleAsync(
        GetAvailableModelsCommand request,
        CancellationToken cancellationToken) =>
        modelCatalogService.GetAvailableModelsAsync(cancellationToken);
}
