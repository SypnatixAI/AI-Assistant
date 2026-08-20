namespace AssistantCore.Service.Application.Configuration;

public sealed class Microsoft365Options
{
    public const string SectionName = "Microsoft365";

    public string AuthorityBaseUrl { get; init; } = "https://login.microsoftonline.com";

    public string GraphBaseUrl { get; init; } = "https://graph.microsoft.com";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string ConsentCallbackUrl { get; init; } = string.Empty;

    public int ConsentStateLifetimeMinutes { get; init; } = 10;
}
