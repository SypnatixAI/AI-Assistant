namespace AssistantCore.Service.Infrastructure.AiModels.Configuration;

public sealed class AiModelProviderOptions
{
    public bool Enabled { get; init; }

    public string Endpoint { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; }

    public Dictionary<string, AiModelOptions> Models { get; init; } = [];
}
