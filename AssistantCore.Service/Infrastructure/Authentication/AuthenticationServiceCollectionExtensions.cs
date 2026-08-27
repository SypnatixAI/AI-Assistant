using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Infrastructure.Authentication.Authorization;
using AssistantCore.Service.Infrastructure.Authentication.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IIdentityClaimsMapper, MicrosoftEntraIdentityClaimsMapper>();
        services.AddScoped<ICurrentIdentity, HttpCurrentIdentity>();

        services.AddSingleton<IValidateOptions<ApiAccessOptions>, ApiAccessOptionsValidator>();
        services.AddOptions<ApiAccessOptions>()
            .Bind(configuration.GetSection(ApiAccessOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IAuthorizationHandler, RequiredScopeAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, RequiredAppRoleAuthorizationHandler>();
        services.AddSingleton<IConfigureOptions<AuthorizationOptions>, ConfigureApiAuthorizationOptions>();
        services.AddAuthorization();

        return services;
    }
}
