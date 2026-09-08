using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Usage;

namespace AssistantCore.Service.Application.Commands.SetUsagePolicy;

public sealed record SetUsagePolicyCommand(
    Guid OrganizationId,
    long MonthlyTokenLimit,
    string Status,
    string EffectiveAt) : IRequest<UsagePolicyResponse>;
