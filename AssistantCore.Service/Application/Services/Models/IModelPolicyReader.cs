namespace AssistantCore.Service.Application.Services.Models;

/// <summary>
/// Une entree de la politique de modeles applicable a une organisation. Ne transporte
/// jamais de fournisseur, de cle ou d'URL technique.
/// </summary>
public sealed record ModelPolicyEntry(
    string Id,
    string DisplayName,
    string Description);

public sealed record ModelPolicy(
    string DefaultModelId,
    IReadOnlyList<ModelPolicyEntry> Models);

/// <summary>
/// Lit la politique de modeles actifs pour une organisation. La premiere version
/// applique la meme configuration globale a toutes les organisations, mais le
/// parametre organizationId permet d'introduire une politique par client plus tard
/// sans changer le contrat public.
/// </summary>
public interface IModelPolicyReader
{
    Task<ModelPolicy> GetPolicyAsync(Guid organizationId, CancellationToken cancellationToken);
}
