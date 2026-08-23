using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.ExternalServices.Services.Azure;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

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
                    && IsHttpsUrl(options.WebhookBaseUrl)
                    && options.ConsentStateLifetimeMinutes is > 0 and <= 60
                    && options.SubscriptionLifetimeHours is > 1 and <= 48
                    && options.SubscriptionRenewalLeadTimeHours > 0
                    && options.SubscriptionRenewalLeadTimeHours < options.SubscriptionLifetimeHours
                    && options.SynchronizationLeaseMinutes is > 0 and <= 60
                    && options.SynchronizationIntervalMinutes > 0,
                "Microsoft365 requires HTTPS URLs, credentials, valid lifetimes, and valid subscription renewal settings.")
            .ValidateOnStart();

        services.AddOptions<ServiceBusOptions>()
            .Bind(configuration.GetSection(ServiceBusOptions.SectionName))
            .Validate(options =>
                    !string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace)
                    && !string.IsNullOrWhiteSpace(options.DriveSyncQueue)
                    && !string.IsNullOrWhiteSpace(options.ListSyncQueue),
                "ServiceBus namespace and synchronization queue names are required.")
            .ValidateOnStart();

        services.AddDataProtection();
        services.AddMemoryCache();
        services.AddHttpClient<MicrosoftIdentityClient>();
        services.AddHttpClient<MicrosoftGraphClient>();
        services.AddHttpClient<MicrosoftGraphListSchemaClient>();
        services.AddHttpClient<MicrosoftGraphListItemDeltaClient>();
        services.AddHttpClient<MicrosoftGraphDriveItemDeltaClient>();
        services.AddHttpClient<MicrosoftGraphSiteSourcesClient>();
        services.AddHttpClient<MicrosoftGraphSubscriptionClient>();
        services.AddScoped<IMicrosoft365ConsentClient, Microsoft365ConsentClientAdapter>();
        services.AddScoped<IMicrosoft365ListItemDeltaClient, Microsoft365ListItemDeltaClientAdapter>();
        services.AddScoped<IMicrosoft365DriveItemDeltaClient, Microsoft365DriveItemDeltaClientAdapter>();
        services.AddScoped<IMicrosoft365ListSchemaClient, Microsoft365ListSchemaClientAdapter>();
        services.AddScoped<IMicrosoft365SiteSourcesClient, Microsoft365SiteSourcesClientAdapter>();
        services.AddScoped<IMicrosoft365SubscriptionClient, Microsoft365SubscriptionClientAdapter>();
        services.AddSingleton<IMicrosoft365ClientStateProtector, Microsoft365ClientStateProtectorAdapter>();
        services.AddSingleton(serviceProvider =>
        {
            var serviceBusOptions = serviceProvider.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
            return new AzureServiceBusPublisherClient(serviceBusOptions.FullyQualifiedNamespace);
        });
        services.AddSingleton<IMicrosoft365SynchronizationPublisher, Microsoft365SynchronizationPublisherAdapter>();
        services.AddSingleton<IMicrosoft365ConsentStateProtector, Microsoft365ConsentStateProtectorAdapter>();
        services.AddSingleton<IMicrosoft365TechnicalTokenStore, Microsoft365TechnicalTokenStoreAdapter>();

        return services;
    }

    private static bool IsHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
}
