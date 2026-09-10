using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.AuthenticateUser.Models;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Application.Commands.AuthenticateUser;

public sealed class AuthenticateUserCommandHandler(
    IAuthenticateUserService authenticateUserService,
    IAiToolRegistry aiToolRegistry) : IRequestHandler<AuthenticateUserCommand, AuthenticateUserResponse>
{
    public async Task<AuthenticateUserResponse> HandleAsync(AuthenticateUserCommand request, CancellationToken cancellationToken)
    {
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);

        // Warm the organization tool cache during authentication so the first
        // message does not pay the connector lookup cost.
        await aiToolRegistry.GetAvailableToolsAsync(
            organization.Id,
            cancellationToken);

        return new AuthenticateUserResponse(
            new CurrentUserResponse(
                member.Id,
                member.Name,
                member.Email),
            new CurrentOrganizationResponse(
                organization.Id,
                organization.Name),
            [member.Role.ToString()]);
    }
}
