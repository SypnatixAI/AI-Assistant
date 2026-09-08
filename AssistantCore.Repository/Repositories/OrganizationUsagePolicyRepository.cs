using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories.Audit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Repositories;

public sealed class OrganizationUsagePolicyRepository(
    AssistantCoreDbContext dbContext,
    IAdministrativeAuditRepository administrativeAuditRepository) : IOrganizationUsagePolicyRepository
{
    private const int UniqueConstraintViolation = 2627;
    private const int DuplicateIndexKeyViolation = 2601;

    public async Task<UsagePolicyCreateResult> CreateAsync(
        Guid organizationId,
        long monthlyTokenLimit,
        UsagePolicyStatus status,
        DateTimeOffset effectiveAt,
        Guid actorId,
        DateTimeOffset createdAt,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var latest = await dbContext.OrganizationUsagePolicies
            .AsNoTracking()
            .Where(policy => policy.OrganizationId == organizationId)
            .OrderByDescending(policy => policy.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var policy = new OrganizationUsagePolicy
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Version = (latest?.Version ?? 0) + 1,
            MonthlyTokenLimit = monthlyTokenLimit,
            Status = status,
            EffectiveAt = effectiveAt,
            CreatedAt = createdAt,
            ActorId = actorId
        };

        dbContext.OrganizationUsagePolicies.Add(policy);

        administrativeAuditRepository.Stage(AdministrativeAuditEntryFactory.Create(
            organizationId,
            AdministrativeAuditSubjectTypes.Application,
            actorId,
            AdministrativeAuditAction.UsagePolicyChanged,
            AdministrativeAuditSubjectTypes.Organization,
            organizationId,
            createdAt,
            latest is null ? new Dictionary<string, object?>() : ToAuditValues(latest),
            ToAuditValues(policy),
            correlationId));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsConflictOn(
            exception,
            "IX_OrganizationUsagePolicy_OrganizationId_Version"))
        {
            dbContext.Entry(policy).State = EntityState.Detached;
            return UsagePolicyCreateResult.VersionConflict;
        }
        catch (DbUpdateException exception) when (IsConflictOn(
            exception,
            "IX_OrganizationUsagePolicy_OrganizationId_EffectiveAt"))
        {
            dbContext.Entry(policy).State = EntityState.Detached;
            return UsagePolicyCreateResult.EffectiveAtConflict;
        }

        return UsagePolicyCreateResult.Created(policy);
    }

    public async Task<OrganizationUsagePolicy?> FindEffectiveAsync(
        Guid organizationId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default) =>
        await dbContext.OrganizationUsagePolicies
            .AsNoTracking()
            .Where(policy => policy.OrganizationId == organizationId && policy.EffectiveAt <= asOf)
            .OrderByDescending(policy => policy.EffectiveAt)
            .ThenByDescending(policy => policy.Version)
            .FirstOrDefaultAsync(cancellationToken);

    private static Dictionary<string, object?> ToAuditValues(OrganizationUsagePolicy policy) =>
        new()
        {
            ["monthlyTokenLimit"] = policy.MonthlyTokenLimit,
            ["status"] = policy.Status.ToString(),
            ["effectiveAt"] = policy.EffectiveAt.ToString("O"),
            ["version"] = policy.Version
        };

    private static bool IsConflictOn(DbUpdateException exception, string indexName) =>
        exception.InnerException is SqlException
        {
            Number: UniqueConstraintViolation or DuplicateIndexKeyViolation
        } sqlException
        && sqlException.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase);
}
