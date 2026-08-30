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
        var service = new OrganizationManagementService(repository);

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
        var service = new OrganizationManagementService(repository);

        // When
        var action = () => service.CreateOrganizationAsync(domain);

        // Then
        await Assert.ThrowsAsync<BadRequestException>(action);
        Assert.Null(repository.ReceivedOrganization);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnExistingDomain_When_CreateOrganizationAsync_Then_ThrowsConflict(
        string domain)
    {
        // Given
        var repository = new StubOrganizationRepository { SimulateConflict = true };
        var service = new OrganizationManagementService(repository);

        // When
        var action = () => service.CreateOrganizationAsync(domain);

        // Then
        var exception = await Assert.ThrowsAsync<ConflictException>(action);
        Assert.Equal(
            "An organization already exists for this identity provider and domain.",
            exception.Message);
    }
}
