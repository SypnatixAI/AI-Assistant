using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Organizations;

namespace AssistantCore.Service.Application.Commands.CreateOrganization;

public sealed record CreateOrganizationCommand(
    string Name,
    string IdentityProvider,
    string ExternalTenantId) : IRequest<OrganizationResponse>;
