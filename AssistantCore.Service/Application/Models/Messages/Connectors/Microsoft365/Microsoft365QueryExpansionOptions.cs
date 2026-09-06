namespace AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

public sealed record Microsoft365QueryExpansionOptions(
    bool Enabled,
    int MaximumGeneratedQueries,
    int TimeoutMilliseconds,
    int MinimumWordCountForExpansion)
{
    public static Microsoft365QueryExpansionOptions Disabled { get; } = new(
        false,
        0,
        1500,
        5);
}
