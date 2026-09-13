using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Models.Authentication;
using AssistantCore.Service.Application.Models.Messages.Connectors;

namespace AssistantCore.Service.Application.Models.Messages;

public sealed record MessageUserContext(
    Organization Organization,
    OrganizationMember Member,
    AuthenticatedIdentity? Identity = null)
{
    public ConnectorExecutionContext CreateConnectorExecutionContext() =>
        new(
            Organization.Id,
            Member.Id,
            ResolveExternalTenantId(),
            Guid.TryParse(Identity?.ExternalUserId ?? Member.ExternalUserId, out var entraUserId)
                ? entraUserId
                : null,
            Identity?.Provider ?? Member.IdentityProvider,
            UserEmail: ResolveUserEmail());

    private string? ResolveExternalTenantId()
    {
        var currentExternalTenantId = Identity?.ExternalOrganizationId;
        return !string.IsNullOrWhiteSpace(currentExternalTenantId)
            ? currentExternalTenantId
            : Organization.ExternalTenantId;
    }

    private string? ResolveUserEmail()
    {
        var currentEmail = Identity?.Email;
        return !string.IsNullOrWhiteSpace(currentEmail)
            ? currentEmail
            : Member.Email;
    }
}
