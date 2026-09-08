namespace AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

public sealed record Microsoft365ConnectorOptions(
    int MaximumResults,
    int MaximumContentLength,
    Microsoft365QueryExpansionOptions QueryExpansion,
    Microsoft365AgenticRetrievalOptions AgenticRetrieval)
{
    public Microsoft365ConnectorOptions(
        int maximumResults,
        int maximumContentLength)
        : this(
            maximumResults,
            maximumContentLength,
            Microsoft365QueryExpansionOptions.Disabled,
            Microsoft365AgenticRetrievalOptions.Default)
    {
    }

    public Microsoft365ConnectorOptions(
        int maximumResults,
        int maximumContentLength,
        Microsoft365QueryExpansionOptions queryExpansion)
        : this(
            maximumResults,
            maximumContentLength,
            queryExpansion,
            Microsoft365AgenticRetrievalOptions.Default)
    {
    }
}
