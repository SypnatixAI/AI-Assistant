namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public sealed class Microsoft365QueryExpansionBypassPolicy(int minimumWordCount)
    : IMicrosoft365QueryExpansionBypassPolicy
{
    public bool ShouldExpand(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var normalizedQuery = query.Trim();
        return CountWords(normalizedQuery) >= minimumWordCount;
    }

    private static int CountWords(string query) =>
        query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
}
