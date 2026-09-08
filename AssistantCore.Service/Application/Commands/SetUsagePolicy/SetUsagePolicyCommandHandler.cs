using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Usage;
using AssistantCore.Service.Application.Services.Usage;

namespace AssistantCore.Service.Application.Commands.SetUsagePolicy;

public sealed class SetUsagePolicyCommandHandler(
    IUsagePolicyManagementService usagePolicyManagementService)
    : IRequestHandler<SetUsagePolicyCommand, UsagePolicyResponse>
{
    public async Task<UsagePolicyResponse> HandleAsync(
        SetUsagePolicyCommand request,
        CancellationToken cancellationToken)
    {
        var policy = await usagePolicyManagementService.SetUsagePolicyAsync(
            request.OrganizationId,
            request.MonthlyTokenLimit,
            request.Status,
            request.EffectiveAt,
            cancellationToken);

        return UsagePolicyResponse.FromPolicy(policy);
    }
}
