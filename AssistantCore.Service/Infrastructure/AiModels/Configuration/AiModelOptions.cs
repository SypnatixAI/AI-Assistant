namespace AssistantCore.Service.Infrastructure.AiModels.Configuration;

public sealed class AiModelOptions
{
    public bool Enabled { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
