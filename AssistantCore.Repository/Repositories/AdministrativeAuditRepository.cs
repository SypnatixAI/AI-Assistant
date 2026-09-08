using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Persistence;

namespace AssistantCore.Repository.Repositories;

public sealed class AdministrativeAuditRepository(AssistantCoreDbContext dbContext)
    : IAdministrativeAuditRepository
{
    public void Stage(AdministrativeAuditEntry entry)
    {
        dbContext.AdministrativeAuditEntries.Add(entry);
    }
}
