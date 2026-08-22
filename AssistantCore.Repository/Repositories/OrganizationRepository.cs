using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Repositories;

public sealed class OrganizationRepository(AssistantCoreDbContext dbContext) : IOrganizationRepository
{
    private const int UniqueConstraintViolation = 2627;
    private const int DuplicateIndexKeyViolation = 2601;

    public async Task<Organization?> TryCreateOrganizationAsync(
        Organization organization,
        CancellationToken cancellationToken = default)
    {
        dbContext.Organizations.Add(organization);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return organization;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(organization).State = EntityState.Detached;
            return null;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException
        {
            Number: UniqueConstraintViolation or DuplicateIndexKeyViolation
        };
}
