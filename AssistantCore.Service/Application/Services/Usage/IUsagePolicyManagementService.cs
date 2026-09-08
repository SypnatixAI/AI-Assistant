using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Services.Usage;

public interface IUsagePolicyManagementService
{
    Task<OrganizationUsagePolicy> SetUsagePolicyAsync(
        Guid organizationId,
        long monthlyTokenLimit,
        string status,
        string effectiveAt,
        CancellationToken cancellationToken = default);
}
