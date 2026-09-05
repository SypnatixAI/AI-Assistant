namespace AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

public sealed record Microsoft365SearchSecurityContext(
    Guid OrganizationId,
    string EntraUserId,
    IReadOnlyCollection<string> EntraGroupIds,
    IReadOnlyCollection<string> SharePointGroupIds)
{
    public Microsoft365SearchSecurityContext(
        Guid organizationId,
        string entraUserId,
        IReadOnlyCollection<string> entraGroupIds)
        : this(organizationId, entraUserId, entraGroupIds, [])
    {
    }
}
