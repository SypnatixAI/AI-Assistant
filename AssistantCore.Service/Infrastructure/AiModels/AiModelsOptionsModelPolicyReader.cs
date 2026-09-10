using AssistantCore.Service.Application.Services.Models;
using AssistantCore.Service.Infrastructure.AiModels.Configuration;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.AiModels;

/// <summary>
/// Traduit la configuration technique AiModelsOptions en politique applicative, sans
/// jamais exposer le fournisseur, l'URL ou la cle d'un modele.
/// </summary>
public sealed class AiModelsOptionsModelPolicyReader(
    IOptions<AiModelsOptions> options) : IModelPolicyReader
{
    public Task<ModelPolicy> GetPolicyAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        var activeModels = configuration.Providers.Values
            .Where(provider => provider.Enabled)
            .SelectMany(provider => provider.Models)
            .Where(model => model.Value.Enabled)
            .Select(model => new ModelPolicyEntry(
                model.Key,
                model.Value.DisplayName,
                model.Value.Description))
            .ToArray();

        return Task.FromResult(new ModelPolicy(configuration.DefaultModel, activeModels));
    }
}
