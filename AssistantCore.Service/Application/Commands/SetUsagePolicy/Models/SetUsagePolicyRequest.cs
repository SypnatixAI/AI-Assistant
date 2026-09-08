namespace AssistantCore.Service.Application.Commands.SetUsagePolicy.Models;

public sealed record SetUsagePolicyRequest(
    long MonthlyTokenLimit,
    string Status,
    string EffectiveAt);
