namespace AssistantCore.Service.Infrastructure.AiModels.Configuration;

public sealed class AiModelsOptions
{
    public const string SectionName = "AiModels";

    public string DefaultModel { get; init; } = string.Empty;

    public Dictionary<string, AiModelProviderOptions> Providers { get; init; } = [];
}
