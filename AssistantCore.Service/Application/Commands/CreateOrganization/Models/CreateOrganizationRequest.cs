namespace AssistantCore.Service.Application.Commands.CreateOrganization.Models;

public sealed record CreateOrganizationRequest(
    string Name,
    string IdentityProvider,
    string ExternalTenantId);
