namespace AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

public sealed record Microsoft365SearchSecurityContext(
    Guid OrganizationId,
    string EntraUserId,
    IReadOnlyCollection<string> EntraGroupIds);
