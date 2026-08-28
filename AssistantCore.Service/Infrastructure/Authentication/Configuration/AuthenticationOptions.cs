namespace AssistantCore.Service.Infrastructure.Authentication.Configuration;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";
}

public sealed class LocalJwtOptions
{
    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;
}
