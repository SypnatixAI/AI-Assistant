using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Repositories;

public interface ITokenConsumptionRepository
{
    /// <summary>
    /// Enregistre la consommation. Retourne false sans lever d'exception si un
    /// enregistrement existe deja pour ce message Assistant (contrainte unique),
    /// afin qu'un replay de message ne compte jamais deux fois les memes jetons.
    /// </summary>
    Task<bool> TryRecordConsumptionAsync(
        TokenConsumption consumption,
        CancellationToken cancellationToken = default);

    Task<long> SumTokensForPeriodAsync(
        Guid organizationId,
        DateTimeOffset periodStartsAt,
        DateTimeOffset periodEndsAt,
        CancellationToken cancellationToken = default);
}
