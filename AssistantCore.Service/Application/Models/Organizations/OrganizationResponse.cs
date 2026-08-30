using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Models.Organizations;

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string Domain,
    string IdentityProvider,
    string? ExternalTenantId,
    string Status)
{
    public static OrganizationResponse FromOrganization(Organization organization) =>
        new(
            organization.Id,
            organization.Name,
            organization.Domain,
            organization.IdentityProvider.ToString(),
            organization.ExternalTenantId,
            organization.Status.ToString());
}
