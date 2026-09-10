namespace AssistantCore.Service.Application.Models.Models;

public sealed record AvailableModelResponse(
    string Id,
    string DisplayName,
    string Description,
    bool IsDefault);

public sealed record GetAvailableModelsResponse(
    string DefaultModelId,
    IReadOnlyList<AvailableModelResponse> Models);
