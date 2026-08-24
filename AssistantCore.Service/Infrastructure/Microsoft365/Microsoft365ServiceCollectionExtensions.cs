using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.ExternalServices.Services.Azure;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public static class Microsoft365ServiceCollectionExtensions
{
    private static readonly string[] SensitiveHttpHeaders =
        ["Authorization", "Cookie", "Set-Cookie"];

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
                    && options.SynchronizationIntervalMinutes > 0
                    && options.AclReconciliationIntervalMinutes > 0
                    && options.AclReconciliationRetryMinutes > 0
                    && options.AclReconciliationBatchSize is > 0 and <= 1000,
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

        services.AddOptions<AzureAiSearchOptions>()
            .Bind(configuration.GetSection(AzureAiSearchOptions.SectionName));

        services.AddDataProtection();
        services.AddMemoryCache();
        AddProtectedHttpClient<MicrosoftIdentityClient>(services);
        services.AddHttpClient<MicrosoftGraphClient>();
        services.AddHttpClient<MicrosoftGraphListSchemaClient>();
        services.AddHttpClient<MicrosoftGraphListItemDeltaClient>();
        services.AddHttpClient<MicrosoftGraphDriveItemDeltaClient>();
        services.AddHttpClient<MicrosoftGraphSiteSourcesClient>();
        services.AddHttpClient<MicrosoftGraphSubscriptionClient>();
        AddProtectedHttpClient<MicrosoftGraphUserGroupClient>(services);
        AddProtectedHttpClient<MicrosoftGraphDriveItemPermissionClient>(services);
        AddProtectedHttpClient<MicrosoftSharePointListItemPermissionClient>(services);
        services.AddHttpClient<AzureAiSearchPassageAclClient>()
            .RedactLoggedHeaders(["api-key", "Authorization"]);
        services.AddHttpClient<AzureAiSearchPassageSearchClient>()
            .RedactLoggedHeaders(["api-key", "Authorization"]);
        services.AddScoped<IMicrosoft365ConsentClient, Microsoft365ConsentClientAdapter>();
        services.AddScoped<IMicrosoft365ListItemDeltaClient, Microsoft365ListItemDeltaClientAdapter>();
        services.AddScoped<IMicrosoft365DriveItemDeltaClient, Microsoft365DriveItemDeltaClientAdapter>();
        services.AddScoped<IMicrosoft365ListSchemaClient, Microsoft365ListSchemaClientAdapter>();
        services.AddScoped<IMicrosoft365SiteSourcesClient, Microsoft365SiteSourcesClientAdapter>();
        services.AddScoped<IMicrosoft365SubscriptionClient, Microsoft365SubscriptionClientAdapter>();
        services.AddScoped<IMicrosoft365AclResolver, Microsoft365AclResolverAdapter>();
        services.AddScoped<IMicrosoft365UserGroupResolver, Microsoft365UserGroupResolverAdapter>();
        services.AddScoped<IMicrosoft365PassageAclWriter, Microsoft365PassageAclWriterAdapter>();
        services.AddScoped<IMicrosoft365PassageIndexWriter, Microsoft365PassageIndexWriterAdapter>();
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

    private static void AddProtectedHttpClient<TClient>(IServiceCollection services)
        where TClient : class =>
        services.AddHttpClient<TClient>()
            .RedactLoggedHeaders(SensitiveHttpHeaders)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false
            });
}
