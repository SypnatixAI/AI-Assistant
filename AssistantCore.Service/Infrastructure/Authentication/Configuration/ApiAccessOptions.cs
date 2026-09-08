namespace AssistantCore.Service.Infrastructure.Authentication.Configuration;

public sealed class ApiAccessOptions
{
    public const string SectionName = "AzureAd";

    public string RequiredScope { get; init; } = string.Empty;

    public string RequiredAdmissionRole { get; init; } = string.Empty;

    public string TenantAdminRole { get; init; } = string.Empty;

    public string UsagePolicyManagementRole { get; init; } = string.Empty;
}
