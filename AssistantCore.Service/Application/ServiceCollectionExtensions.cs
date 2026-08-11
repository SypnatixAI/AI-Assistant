using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Services.Members;
using AssistantCore.Service.Application.Services.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticateUserService, AuthenticateUserService>();
        services.AddScoped<IMemberManagementService, MemberManagementService>();
        services.AddScoped<ISendMessageService, SendMessageService>();

        return services;
    }
}
