using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class OrganizationRepositoryTests
{
    [Theory, AutoDomainData]
    public async Task Given_ANewOrganization_When_TryCreateOrganizationAsync_Then_PersistsAndReturnsOrganization(
        Organization organization,
        int _)
    {
        // Given
        organization.Domain = string.IsNullOrWhiteSpace(organization.Domain) ? "contoso.com" : organization.Domain;
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new AssistantCoreDbContext(options);
        var repository = new OrganizationRepository(dbContext);

        // When
        var result = await repository.TryCreateOrganizationAsync(organization, CancellationToken.None);

        // Then
        Assert.Same(organization, result);
        var persistedOrganization = await dbContext.Organizations.SingleAsync();
        Assert.Equal(organization.Id, persistedOrganization.Id);
        Assert.Equal(organization.Name, persistedOrganization.Name);
        Assert.Equal(organization.Domain, persistedOrganization.Domain);
        Assert.Equal(organization.IdentityProvider, persistedOrganization.IdentityProvider);
        Assert.Equal(organization.ExternalTenantId, persistedOrganization.ExternalTenantId);
        Assert.Equal(organization.Status, persistedOrganization.Status);
    }
}
