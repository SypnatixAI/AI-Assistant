namespace AssistantCore.Service.Application.Models.Messages.Connectors.InternalData;

public sealed record InternalDataConnectorOptions(
    IReadOnlySet<InternalDataCategory> EnabledCategories,
    int MaximumResults,
    int MaximumContentLength);
