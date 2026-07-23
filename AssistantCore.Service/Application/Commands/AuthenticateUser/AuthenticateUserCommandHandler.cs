using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Authorization;
using AssistantCore.Service.Application.Commands.AuthenticateUser.Models;
using AssistantCore.Service.Application.Services.AuthenticateUser;

namespace AssistantCore.Service.Application.Commands.AuthenticateUser;

public sealed class AuthenticateUserCommandHandler(
    IAuthenticateUserService authenticateUserService,
    IRolePermissionService rolePermissionService) : IRequestHandler<AuthenticateUserCommand, AuthenticateUserResponse>
{
    public async Task<AuthenticateUserResponse> HandleAsync(AuthenticateUserCommand request, CancellationToken cancellationToken)
    {
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);
        var permissions = rolePermissionService.GetPermissions(member.Role);

        return new AuthenticateUserResponse(
            new CurrentUserResponse(
                member.Id,
                member.Name,
                member.Email),
            new CurrentOrganizationResponse(
                organization.Id,
                organization.Name),
            [member.Role.ToString()],
            permissions.Select(permission => permission.ToString()).ToArray());
    }
}
