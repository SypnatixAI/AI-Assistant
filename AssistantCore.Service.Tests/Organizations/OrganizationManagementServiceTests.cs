using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Organizations;

namespace AssistantCore.Service.Tests.Organizations;

public sealed class OrganizationManagementServiceTests
{
    [Theory, InlineAutoDomainData(" Contoso.com ")]
    public async Task Given_ValidDomain_When_CreateOrganizationAsync_Then_CreatesAnActiveNormalizedOrganization(
        string domain,
        CancellationToken cancellationToken)
    {
        // Given
        var repository = new StubOrganizationRepository();
        var service = new OrganizationManagementService(repository, new StubOrganizationQueries());

        // When
        var organization = await service.CreateOrganizationAsync(domain, cancellationToken);

        // Then
        Assert.NotEqual(Guid.Empty, organization.Id);
        Assert.Equal("contoso.com", organization.Name);
        Assert.Equal("contoso.com", organization.Domain);
        Assert.Equal(IdentityProvider.MicrosoftEntraId, organization.IdentityProvider);
        Assert.Null(organization.ExternalTenantId);
        Assert.Equal(RecordStatus.Active, organization.Status);
        Assert.Same(organization, repository.ReceivedOrganization);
        Assert.Equal(cancellationToken, repository.ReceivedCancellationToken);
    }

    [Theory, InlineAutoDomainData("")]
    public async Task Given_AMissingRequiredValue_When_CreateOrganizationAsync_Then_ThrowsBadRequest(
        string domain)
    {
        // Given
        var repository = new StubOrganizationRepository();
        var service = new OrganizationManagementService(repository, new StubOrganizationQueries());

        // When
        var action = () => service.CreateOrganizationAsync(domain);

        // Then
        await Assert.ThrowsAsync<BadRequestException>(action);
        Assert.Null(repository.ReceivedOrganization);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnExistingDomain_When_CreateOrganizationAsync_Then_ReturnsTheExistingOrganization(
        string domain,
        Organization existingOrganization,
        CancellationToken cancellationToken)
    {
        // Given
        existingOrganization.Domain = domain.Trim().ToLowerInvariant();
        existingOrganization.IdentityProvider = IdentityProvider.MicrosoftEntraId;
        existingOrganization.Status = RecordStatus.Active;
        var repository = new StubOrganizationRepository { SimulateConflict = true };
        var queries = new StubOrganizationQueries { DomainResult = existingOrganization };
        var service = new OrganizationManagementService(repository, queries);

        // When
        var result = await service.CreateOrganizationAsync(domain, cancellationToken);

        // Then
        Assert.Same(existingOrganization, result);
        Assert.Equal(IdentityProvider.MicrosoftEntraId, queries.ReceivedIdentityProvider);
        Assert.Equal(existingOrganization.Domain, queries.ReceivedDomain);
        Assert.Equal(cancellationToken, queries.ReceivedCancellationToken);
    }
}
