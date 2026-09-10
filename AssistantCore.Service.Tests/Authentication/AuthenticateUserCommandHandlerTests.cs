using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.AuthenticateUser;
using AssistantCore.Service.Application.Services.AuthenticateUser;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class AuthenticateUserCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnAuthenticatedMember_When_HandleAsync_Then_MapsAuthenticationResponse(
        CancellationToken cancellationToken,
        Organization organization,
        OrganizationMember member)
    {
        // Given
        member.OrganizationId = organization.Id;
        member.Role = OrganizationRole.Admin;
        organization.ExternalTenantId = "tenant-id";
        member.ExternalUserId = Guid.NewGuid().ToString("D");
        var service = new StubAuthenticateUserService { Result = (organization, member) };
        var warmupQueue = new StubAuthenticationCacheWarmupQueue();
        var handler = new AuthenticateUserCommandHandler(service, warmupQueue);

        // When
        var response = await handler.HandleAsync(new AuthenticateUserCommand(), cancellationToken);

        // Then
        Assert.Equal(member.Id, response.User.Id);
        Assert.Equal(member.Name, response.User.DisplayName);
        Assert.Equal(member.Email, response.User.Email);
        Assert.Equal(organization.Id, response.Organization.Id);
        Assert.Equal(organization.Name, response.Organization.Name);
        Assert.Equal(["Admin"], response.Roles);
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
        Assert.Equal(organization.Id, warmupQueue.ReceivedOrganizationId);
        Assert.Equal(organization.ExternalTenantId, warmupQueue.ReceivedExternalTenantId);
        Assert.Equal(member.ExternalUserId, warmupQueue.ReceivedEntraUserId);
    }

    private sealed class StubAuthenticationCacheWarmupQueue : IAuthenticationCacheWarmupQueue
    {
        public Guid? ReceivedOrganizationId { get; private set; }

        public string? ReceivedExternalTenantId { get; private set; }

        public string? ReceivedEntraUserId { get; private set; }

        public void TryQueue(
            Guid organizationId,
            string? externalTenantId,
            string entraUserId)
        {
            ReceivedOrganizationId = organizationId;
            ReceivedExternalTenantId = externalTenantId;
            ReceivedEntraUserId = entraUserId;
        }
    }
}
