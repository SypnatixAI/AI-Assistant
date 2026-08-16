namespace AssistantCore.Service.Application.Models.Messages.Connectors;

public sealed record ConnectorExecutionContext(
    Guid OrganizationId,
    Guid MemberId);
