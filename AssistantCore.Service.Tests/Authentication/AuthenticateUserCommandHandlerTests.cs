using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.AuthenticateUser;

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
        var service = new StubAuthenticateUserService { Result = (organization, member) };
        var handler = new AuthenticateUserCommandHandler(service);

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
    }
}
