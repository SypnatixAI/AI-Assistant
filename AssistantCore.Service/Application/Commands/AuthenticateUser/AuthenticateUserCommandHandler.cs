using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.AuthenticateUser.Models;
using AssistantCore.Service.Application.Services.AuthenticateUser;

namespace AssistantCore.Service.Application.Commands.AuthenticateUser;

public sealed class AuthenticateUserCommandHandler(
    IAuthenticateUserService authenticateUserService,
    IAuthenticationCacheWarmupQueue cacheWarmupQueue) : IRequestHandler<AuthenticateUserCommand, AuthenticateUserResponse>
{
    public async Task<AuthenticateUserResponse> HandleAsync(AuthenticateUserCommand request, CancellationToken cancellationToken)
    {
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);

        // Queue cache warmups after authentication without extending the login
        // response path. The hosted worker owns its dependency-injection scope.
        cacheWarmupQueue.TryQueue(
            organization.Id,
            organization.ExternalTenantId,
            member.ExternalUserId);

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
