namespace AssistantCore.Service.Application.Models.Messages.Connectors.InternalData;

public sealed record InternalDataSearchParameters(
    Guid OrganizationId,
    Guid MemberId,
    string Query,
    IReadOnlySet<InternalDataCategory> Categories,
    int MaximumResults);
