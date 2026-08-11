using AssistantCore.Service.Application.Abstractions;

namespace AssistantCore.Service.Infrastructure.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationInfrastructure(
        this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IIdentityClaimsMapper, MicrosoftEntraIdentityClaimsMapper>();
        services.AddScoped<ICurrentIdentity, HttpCurrentIdentity>();

        return services;
    }
}
