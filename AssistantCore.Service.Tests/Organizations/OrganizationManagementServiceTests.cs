using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Organizations;

namespace AssistantCore.Service.Tests.Organizations;

public sealed class OrganizationManagementServiceTests
{
    [Theory, InlineAutoDomainData(" Contoso ", "microsoftentraid", " tenant-id ")]
    public async Task Given_ValidValues_When_CreateOrganizationAsync_Then_CreatesAnActiveNormalizedOrganization(
        string name,
        string identityProvider,
        string externalTenantId,
        CancellationToken cancellationToken)
    {
        // Given
        var repository = new StubOrganizationRepository();
        var service = new OrganizationManagementService(repository);

        // When
        var organization = await service.CreateOrganizationAsync(
            name,
            identityProvider,
            externalTenantId,
            cancellationToken);

        // Then
        Assert.NotEqual(Guid.Empty, organization.Id);
        Assert.Equal("Contoso", organization.Name);
        Assert.Equal(IdentityProvider.MicrosoftEntraId, organization.IdentityProvider);
        Assert.Equal("tenant-id", organization.ExternalTenantId);
        Assert.Equal(RecordStatus.Active, organization.Status);
        Assert.Same(organization, repository.ReceivedOrganization);
        Assert.Equal(cancellationToken, repository.ReceivedCancellationToken);
    }

    [Theory]
    [InlineAutoDomainData("", "MicrosoftEntraId", "tenant-id")]
    [InlineAutoDomainData("Contoso", "MicrosoftEntraId", "")]
    public async Task Given_AMissingRequiredValue_When_CreateOrganizationAsync_Then_ThrowsBadRequest(
        string name,
        string identityProvider,
        string externalTenantId)
    {
        // Given
        var repository = new StubOrganizationRepository();
        var service = new OrganizationManagementService(repository);

        // When
        var action = () => service.CreateOrganizationAsync(name, identityProvider, externalTenantId);

        // Then
        await Assert.ThrowsAsync<BadRequestException>(action);
        Assert.Null(repository.ReceivedOrganization);
    }

    [Theory]
    [InlineAutoDomainData("Contoso", "Unsupported", "tenant-id")]
    [InlineAutoDomainData("Contoso", "1", "tenant-id")]
    public async Task Given_AnUnsupportedProvider_When_CreateOrganizationAsync_Then_ThrowsBadRequest(
        string name,
        string identityProvider,
        string externalTenantId)
    {
        // Given
        var repository = new StubOrganizationRepository();
        var service = new OrganizationManagementService(repository);

        // When
        var action = () => service.CreateOrganizationAsync(name, identityProvider, externalTenantId);

        // Then
        var exception = await Assert.ThrowsAsync<BadRequestException>(action);
        Assert.Equal("Identity provider must be 'MicrosoftEntraId'.", exception.Message);
        Assert.Null(repository.ReceivedOrganization);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnExistingTenant_When_CreateOrganizationAsync_Then_ThrowsConflict(
        string name,
        string externalTenantId)
    {
        // Given
        var repository = new StubOrganizationRepository { SimulateConflict = true };
        var service = new OrganizationManagementService(repository);

        // When
        var action = () => service.CreateOrganizationAsync(
            name,
            IdentityProvider.MicrosoftEntraId.ToString(),
            externalTenantId);

        // Then
        var exception = await Assert.ThrowsAsync<ConflictException>(action);
        Assert.Equal(
            "An organization already exists for this identity provider and external tenant.",
            exception.Message);
    }
}
