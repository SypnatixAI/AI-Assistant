using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Infrastructure.Authentication.Authorization;
using AssistantCore.Service.Infrastructure.Authentication.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Text;

namespace AssistantCore.Service.Infrastructure.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    private const string LocalEnvironmentName = "Local";
    private const string LocalJwtMode = "LocalJwt";
    private const string MicrosoftEntraMode = "MicrosoftEntra";

    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var mode = configuration[$"{AuthenticationOptions.SectionName}:Mode"]
            ?? MicrosoftEntraMode;

        if (string.Equals(mode, MicrosoftEntraMode, StringComparison.OrdinalIgnoreCase))
        {
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(configuration.GetSection(ApiAccessOptions.SectionName));
            return services;
        }

        if (!string.Equals(mode, LocalJwtMode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported authentication mode '{mode}'.");
        }

        if (!environment.IsEnvironment(LocalEnvironmentName))
        {
            throw new InvalidOperationException(
                $"Authentication mode '{LocalJwtMode}' is only allowed in the '{LocalEnvironmentName}' environment.");
        }

        var localJwt = configuration
            .GetSection($"{AuthenticationOptions.SectionName}:LocalJwt")
            .Get<LocalJwtOptions>()
            ?? throw new InvalidOperationException("Local JWT configuration is missing.");
        ValidateLocalJwtOptions(localJwt);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = localJwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = localJwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(localJwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name"
                };
            });

        return services;
    }

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

    private static void ValidateLocalJwtOptions(LocalJwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer)
            || string.IsNullOrWhiteSpace(options.Audience)
            || Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Local JWT requires an issuer, an audience, and a signing key of at least 32 bytes.");
        }
    }
}
