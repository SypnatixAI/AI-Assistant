namespace AssistantCore.Service.Application.Configuration;

public sealed class OrganizationRoleOptions
{
    public const string SectionName = "AzureAd";

    public string RequiredAdmissionRole { get; init; } = string.Empty;

    public string TenantAdminRole { get; init; } = string.Empty;
}
