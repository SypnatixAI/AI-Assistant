using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.AuthenticateUser;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class AuthenticateUserCommandHandlerTests
{
    [Fact]
    public async Task Given_AnAuthenticatedMember_When_HandlingCommand_Then_MapsAuthenticationResponse()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Contoso"
        };
        var member = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Admin User",
            Email = "admin@example.com",
            Role = OrganizationRole.Admin
        };
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
