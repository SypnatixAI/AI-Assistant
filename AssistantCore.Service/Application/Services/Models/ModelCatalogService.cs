using AssistantCore.Service.Application.Models.Models;
using AssistantCore.Service.Application.Services.Messages.Authorization;

namespace AssistantCore.Service.Application.Services.Models;

public sealed class ModelCatalogService(
    IMessageUserContextService userContextService,
    IModelPolicyReader modelPolicyReader) : IModelCatalogService
{
    public async Task<GetAvailableModelsResponse> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var userContext = await userContextService.GetCurrentAsync(cancellationToken);
        var policy = await modelPolicyReader.GetPolicyAsync(
            userContext.Organization.Id,
            cancellationToken);

        if (policy.Models.Count == 0)
        {
            throw new InvalidOperationException("No active AI model is configured.");
        }

        if (policy.Models.All(model => model.Id != policy.DefaultModelId))
        {
            throw new InvalidOperationException("The default model is not part of the active models.");
        }

        var models = policy.Models
            .Select(model => new AvailableModelResponse(
                model.Id,
                model.DisplayName,
                model.Description,
                model.Id == policy.DefaultModelId))
            .ToArray();

        return new GetAvailableModelsResponse(policy.DefaultModelId, models);
    }
}
