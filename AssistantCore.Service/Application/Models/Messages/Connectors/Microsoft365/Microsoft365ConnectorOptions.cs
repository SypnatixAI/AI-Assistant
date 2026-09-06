namespace AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

public sealed record Microsoft365ConnectorOptions(
    int MaximumResults,
    int MaximumContentLength,
    Microsoft365QueryExpansionOptions QueryExpansion)
{
    public Microsoft365ConnectorOptions(
        int maximumResults,
        int maximumContentLength)
        : this(
            maximumResults,
            maximumContentLength,
            Microsoft365QueryExpansionOptions.Disabled)
    {
    }
}
