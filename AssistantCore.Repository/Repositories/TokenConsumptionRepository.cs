using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Repositories;

public sealed class TokenConsumptionRepository(AssistantCoreDbContext dbContext) : ITokenConsumptionRepository
{
    private const int UniqueConstraintViolation = 2627;
    private const int DuplicateIndexKeyViolation = 2601;

    public async Task<bool> TryRecordConsumptionAsync(
        TokenConsumption consumption,
        CancellationToken cancellationToken = default)
    {
        dbContext.TokenConsumptions.Add(consumption);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(consumption).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<long> SumTokensForPeriodAsync(
        Guid organizationId,
        DateTimeOffset periodStartsAt,
        DateTimeOffset periodEndsAt,
        CancellationToken cancellationToken = default) =>
        await dbContext.TokenConsumptions
            .Where(consumption =>
                consumption.OrganizationId == organizationId
                && consumption.PeriodStartsAt == periodStartsAt
                && consumption.PeriodEndsAt == periodEndsAt)
            .SumAsync(consumption => (long?)consumption.TotalTokens, cancellationToken)
            ?? 0L;

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException
        {
            Number: UniqueConstraintViolation or DuplicateIndexKeyViolation
        };
}
