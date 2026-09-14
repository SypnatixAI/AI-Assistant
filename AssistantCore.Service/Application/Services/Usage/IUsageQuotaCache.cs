using AssistantCore.Service.Application.Models.Usage;

namespace AssistantCore.Service.Application.Services.Usage;

public interface IUsageQuotaCache
{
    void Initialize(
        DateTimeOffset periodStartsAt,
        DateTimeOffset periodEndsAt,
        IReadOnlyDictionary<Guid, long> tokensUsedByOrganization);

    void EnsureQuotaAvailable(Guid organizationId, DateTimeOffset now);

    MessageUsageResponse RecordConsumption(
        Guid organizationId,
        long requestTokens,
        DateTimeOffset occurredAt);
}
