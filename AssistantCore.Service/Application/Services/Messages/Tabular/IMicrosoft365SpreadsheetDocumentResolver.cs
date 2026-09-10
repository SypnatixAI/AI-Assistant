using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tabular;

namespace AssistantCore.Service.Application.Services.Messages.Tabular;

public interface IMicrosoft365SpreadsheetDocumentResolver
{
    Task<Microsoft365SpreadsheetDocument> ResolveAsync(
        string fileName,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken);
}
