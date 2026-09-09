namespace AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

public sealed record Microsoft365AgenticRetrievalOptions(
    bool Enabled,
    int MaxRuntimeInSeconds,
    int MaxOutputSizeInTokens)
{
    public static Microsoft365AgenticRetrievalOptions Default { get; } = new(
        true,
        30,
        6000);
}
