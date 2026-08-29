namespace AssistantCore.Service.Infrastructure.Cors;

public sealed class ApiCorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = [];
}
