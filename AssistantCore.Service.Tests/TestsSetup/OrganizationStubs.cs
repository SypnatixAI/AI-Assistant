using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Services.Organizations;

namespace AssistantCore.Service.Tests;

internal sealed class StubOrganizationRepository : IOrganizationRepository
{
    public bool SimulateConflict { get; init; }

    public Organization? ReceivedOrganization { get; private set; }

    public Organization? AssociatedOrganization { get; set; }

    public Guid? ReceivedAssociationOrganizationId { get; private set; }

    public IdentityProvider? ReceivedAssociationIdentityProvider { get; private set; }

    public string? ReceivedAssociationExternalTenantId { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<Organization?> TryCreateOrganizationAsync(
        Organization organization,
        CancellationToken cancellationToken = default)
    {
        ReceivedOrganization = organization;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(SimulateConflict ? null : organization);
    }

    public Task<Organization?> AssociateExternalTenantIdAsync(
        Guid organizationId,
        IdentityProvider identityProvider,
        string externalTenantId,
        CancellationToken cancellationToken = default)
    {
        ReceivedAssociationOrganizationId = organizationId;
        ReceivedAssociationIdentityProvider = identityProvider;
        ReceivedAssociationExternalTenantId = externalTenantId;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(AssociatedOrganization);
    }
}

internal sealed class StubOrganizationManagementService : IOrganizationManagementService
{
    public required Organization Organization { get; init; }

    public string? ReceivedDomain { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<Organization> CreateOrganizationAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        ReceivedDomain = domain;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(Organization);
    }
}
