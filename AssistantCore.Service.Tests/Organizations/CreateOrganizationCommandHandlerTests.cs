using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.CreateOrganization;

namespace AssistantCore.Service.Tests.Organizations;

public sealed class CreateOrganizationCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_ACreatedOrganization_When_HandleAsync_Then_MapsResponseAndPropagatesRequest(
        CancellationToken cancellationToken,
        Organization organization)
    {
        // Given
        organization.IdentityProvider = IdentityProvider.MicrosoftEntraId;
        organization.Status = RecordStatus.Active;
        var service = new StubOrganizationManagementService { Organization = organization };
        var handler = new CreateOrganizationCommandHandler(service);
        var command = new CreateOrganizationCommand(
            organization.Name,
            organization.IdentityProvider.ToString(),
            organization.ExternalTenantId);

        // When
        var response = await handler.HandleAsync(command, cancellationToken);

        // Then
        Assert.Equal(organization.Id, response.Id);
        Assert.Equal(organization.Name, response.Name);
        Assert.Equal("MicrosoftEntraId", response.IdentityProvider);
        Assert.Equal(organization.ExternalTenantId, response.ExternalTenantId);
        Assert.Equal("Active", response.Status);
        Assert.Equal(command.Name, service.ReceivedName);
        Assert.Equal(command.IdentityProvider, service.ReceivedIdentityProvider);
        Assert.Equal(command.ExternalTenantId, service.ReceivedExternalTenantId);
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
    }
}
