using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public static class Microsoft365ServiceCollectionExtensions
{
    public static IServiceCollection AddMicrosoft365Infrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<Microsoft365Options>()
            .Bind(configuration.GetSection(Microsoft365Options.SectionName))
            .Validate(options =>
                    IsHttpsUrl(options.AuthorityBaseUrl)
                    && IsHttpsUrl(options.GraphBaseUrl)
                    && Guid.TryParse(options.ClientId, out var clientId)
                    && clientId != Guid.Empty
                    && !string.IsNullOrWhiteSpace(options.ClientSecret)
                    && IsHttpsUrl(options.ConsentCallbackUrl)
                    && options.ConsentStateLifetimeMinutes is > 0 and <= 60,
                "Microsoft365 requires HTTPS URLs, a non-empty ClientId and ClientSecret, and a consent state lifetime between 1 and 60 minutes.")
            .ValidateOnStart();

        services.AddDataProtection();
        services.AddMemoryCache();
        services.AddHttpClient<MicrosoftIdentityClient>();
        services.AddHttpClient<MicrosoftGraphClient>();
        services.AddScoped<IMicrosoft365ConsentClient, Microsoft365ConsentClientAdapter>();
        services.AddSingleton<IMicrosoft365ConsentStateProtector, Microsoft365ConsentStateProtectorAdapter>();
        services.AddSingleton<IMicrosoft365TechnicalTokenStore, Microsoft365TechnicalTokenStoreAdapter>();

        return services;
    }

    private static bool IsHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
}
