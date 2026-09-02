using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;

namespace AssistantCore.Service.Application.Services.Organizations;

public sealed class OrganizationManagementService(
    IOrganizationRepository organizationRepository,
    IOrganizationQueries organizationQueries)
    : IOrganizationManagementService
{
    private const int MaximumDomainLength = 200;

    public async Task<Organization> CreateOrganizationAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        var normalizedDomain = ValidateDomain(domain);

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = normalizedDomain,
            Domain = normalizedDomain,
            IdentityProvider = IdentityProvider.MicrosoftEntraId,
            ExternalTenantId = null,
            Status = RecordStatus.Active
        };

        return await organizationRepository.TryCreateOrganizationAsync(organization, cancellationToken)
            ?? await organizationQueries.FindOrganizationByDomain(
                organization.IdentityProvider,
                normalizedDomain,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The existing organization could not be reloaded after a concurrent creation.");
    }

    private static string ValidateDomain(string domain)
    {
        var normalizedDomain = domain?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedDomain))
        {
            throw new BadRequestException("domain is required.");
        }

        if (normalizedDomain.Length > MaximumDomainLength)
        {
            throw new BadRequestException(
                $"domain must contain at most {MaximumDomainLength} characters.");
        }

        return normalizedDomain;
    }
}
