using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Models.Authentication;

public sealed record AuthenticatedIdentity(
    IdentityProvider Provider,
    string ExternalOrganizationId,
    string ExternalUserId,
    string? DisplayName,
    string? Email);
