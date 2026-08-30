using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Models.Messages.Connectors;

public sealed record ConnectorExecutionContext(
    Guid OrganizationId,
    Guid MemberId,
    string? ExternalTenantId = null,
    Guid? EntraUserId = null,
    IdentityProvider? IdentityProvider = null);
