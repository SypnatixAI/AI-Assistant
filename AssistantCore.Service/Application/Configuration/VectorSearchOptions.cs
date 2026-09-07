namespace AssistantCore.Service.Application.Configuration;

public sealed class VectorSearchOptions
{
    public string Metric { get; init; } = "cosine";
}
