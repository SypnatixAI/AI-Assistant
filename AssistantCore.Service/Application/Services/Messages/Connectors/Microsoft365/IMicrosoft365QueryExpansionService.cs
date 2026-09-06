namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public interface IMicrosoft365QueryExpansionService
{
    Task<IReadOnlyCollection<string>> ExpandAsync(
        string query,
        CancellationToken cancellationToken);
}
