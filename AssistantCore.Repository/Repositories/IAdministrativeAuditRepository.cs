using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Repositories;

public interface IAdministrativeAuditRepository
{
    /// <summary>
    /// Suit l'entree pour insertion sans appeler SaveChanges : l'appelant est responsable de
    /// la persister dans la meme transaction que la modification metier qu'elle journalise.
    /// </summary>
    void Stage(AdministrativeAuditEntry entry);
}
