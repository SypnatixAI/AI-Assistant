namespace AssistantCore.Service.Infrastructure.Authentication.Configuration;

public sealed class ApiAccessOptions
{
    public const string SectionName = "AzureAd";

    public string RequiredScope { get; init; } = string.Empty;
}
