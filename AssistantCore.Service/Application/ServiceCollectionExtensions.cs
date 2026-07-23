using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Authorization;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentIdentity, CurrentIdentity>();
        services.AddScoped<IAuthenticateUserService, AuthenticateUserService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();

        return services;
    }
}
