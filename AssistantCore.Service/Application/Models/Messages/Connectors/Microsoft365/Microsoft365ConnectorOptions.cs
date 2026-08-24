namespace AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

public sealed record Microsoft365ConnectorOptions(
    int MaximumResults,
    int MaximumContentLength);
