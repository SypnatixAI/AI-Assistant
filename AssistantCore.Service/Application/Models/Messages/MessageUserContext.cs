using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Models.Messages.Connectors;

namespace AssistantCore.Service.Application.Models.Messages;

public sealed record MessageUserContext(
    Organization Organization,
    OrganizationMember Member)
{
    public ConnectorExecutionContext CreateConnectorExecutionContext() =>
        new(
            Organization.Id,
            Member.Id,
            Organization.ExternalTenantId,
            Guid.TryParse(Member.ExternalUserId, out var entraUserId)
                ? entraUserId
                : null,
            Member.IdentityProvider);
}
