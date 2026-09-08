using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Repositories;

public interface IOrganizationUsagePolicyRepository
{
    /// <summary>
    /// Insere une nouvelle version de politique pour l'organisation, avec le numero de
    /// version suivant calcule automatiquement. N'ecrase ni ne modifie aucune version
    /// existante.
    /// </summary>
    Task<UsagePolicyCreateResult> CreateAsync(
        Guid organizationId,
        long monthlyTokenLimit,
        UsagePolicyStatus status,
        DateTimeOffset effectiveAt,
        Guid actorId,
        DateTimeOffset createdAt,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retourne la version dont la date d'effet est la plus recente parmi celles deja
    /// atteintes a la date donnee, ou null si l'organisation n'a jamais ete configuree.
    /// </summary>
    Task<OrganizationUsagePolicy?> FindEffectiveAsync(
        Guid organizationId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);
}
