namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public interface IMicrosoft365QueryExpansionClient
{
    Task<IReadOnlyCollection<string>> GenerateAsync(
        string query,
        int maximumGeneratedQueries,
        CancellationToken cancellationToken);
}
