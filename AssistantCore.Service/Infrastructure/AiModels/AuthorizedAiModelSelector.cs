using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Infrastructure.AiModels.Configuration;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.AiModels;

public sealed class AuthorizedAiModelSelector(
    IOptions<AiModelsOptions> options) : IAuthorizedAiModelSelector
{
    private readonly AiModelsOptions _options = options.Value;

    public bool IsAvailable(string? requestedModel) =>
        FindModel(requestedModel) is not null;

    public Task<SelectedAiModel> SelectAsync(
        Guid organizationId,
        string? requestedModel,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            FindModel(requestedModel)
            ?? throw new BadRequestException("The requested AI model is not available."));
    }

    private SelectedAiModel? FindModel(string? requestedModel)
    {
        var modelName = string.IsNullOrWhiteSpace(requestedModel)
            ? _options.DefaultModel
            : requestedModel.Trim();

        foreach (var (providerName, provider) in _options.Providers)
        {
            if (!provider.Enabled)
            {
                continue;
            }

            var configuredModel = provider.Models.FirstOrDefault(model =>
                model.Value.Enabled
                && string.Equals(model.Key, modelName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(configuredModel.Key))
            {
                return new SelectedAiModel(providerName, configuredModel.Key);
            }
        }

        return null;
    }
}
