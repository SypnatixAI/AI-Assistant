using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Services.Organizations;

namespace AssistantCore.Service.Tests;

internal sealed class StubOrganizationRepository : IOrganizationRepository
{
    public bool SimulateConflict { get; init; }

    public Organization? ReceivedOrganization { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<Organization?> TryCreateOrganizationAsync(
        Organization organization,
        CancellationToken cancellationToken = default)
    {
        ReceivedOrganization = organization;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(SimulateConflict ? null : organization);
    }
}

internal sealed class StubOrganizationManagementService : IOrganizationManagementService
{
    public required Organization Organization { get; init; }

    public string? ReceivedName { get; private set; }

    public string? ReceivedIdentityProvider { get; private set; }

    public string? ReceivedExternalTenantId { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<Organization> CreateOrganizationAsync(
        string name,
        string identityProvider,
        string externalTenantId,
        CancellationToken cancellationToken = default)
    {
        ReceivedName = name;
        ReceivedIdentityProvider = identityProvider;
        ReceivedExternalTenantId = externalTenantId;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(Organization);
    }
}
