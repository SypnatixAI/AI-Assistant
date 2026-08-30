using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.ExternalServices.Services.Azure;
using AssistantCore.ExternalServices.Services.OpenAI;
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
                    && IsSecureOrLoopbackUrl(options.ConsentSuccessRedirectUrl)
                    && IsSecureOrLoopbackUrl(options.ConsentErrorRedirectUrl)
                    && IsHttpsUrl(options.WebhookBaseUrl)
                    && options.ConsentStateLifetimeMinutes is > 0 and <= 60
                    && options.SubscriptionLifetimeHours is > 1 and <= 48
                    && options.SubscriptionRenewalLeadTimeHours > 0
                    && options.SubscriptionRenewalLeadTimeHours < options.SubscriptionLifetimeHours
                    && options.SynchronizationLeaseMinutes is > 0 and <= 60
                    && options.SynchronizationIntervalMinutes > 0
                    && options.AclReconciliationIntervalMinutes > 0
                    && options.AclReconciliationRetryMinutes > 0
                    && options.AclReconciliationBatchSize is > 0 and <= 1000
                    && options.MaximumExtractionFileSizeBytes > 0
                    && options.MaximumExtractionExpandedSizeBytes >= options.MaximumExtractionFileSizeBytes
                    && options.MaximumExtractedCharacters > 0
                    && options.ChunkMaximumTokens > 0
                    && options.ChunkOverlapTokens >= 0
                    && options.ChunkOverlapTokens < options.ChunkMaximumTokens
                    && options.MaximumChunksPerDocument > 0
                    && IsHttpsUrl(options.EmbeddingEndpoint)
                    && !string.IsNullOrWhiteSpace(options.EmbeddingModel)
                    && options.EmbeddingDimensions > 0
                    && options.EmbeddingBatchSize is > 0 and <= 2048
                    && options.DocumentWorkLeaseMinutes > 0
                    && options.DocumentWorkRetryMinutes > 0
                    && options.DocumentWorkMaximumAttempts > 0,
                "Microsoft365 requires HTTPS URLs, credentials, valid lifetimes, and valid subscription renewal settings.")
            .ValidateOnStart();

        services.AddOptions<ServiceBusOptions>()
            .Bind(configuration.GetSection(ServiceBusOptions.SectionName))
            .Validate(options =>
                    !options.Enabled
                    || (IsServiceBusNamespace(options.FullyQualifiedNamespace)
                        && !string.IsNullOrWhiteSpace(options.DriveSyncQueue)
                        && !string.IsNullOrWhiteSpace(options.ListSyncQueue)),
                "ServiceBus requires a valid namespace and queue names when enabled.")
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
        AddProtectedHttpClient<MicrosoftGraphDriveContentClient>(services);
        services.AddHttpClient<MicrosoftGraphSiteSourcesClient>();
        AddProtectedHttpClient<MicrosoftGraphSiteClient>(services);
        services.AddHttpClient<MicrosoftGraphSubscriptionClient>();
        AddProtectedHttpClient<MicrosoftGraphUserGroupClient>(services);
        AddProtectedHttpClient<MicrosoftGraphDriveItemPermissionClient>(services);
        AddProtectedHttpClient<MicrosoftSharePointListItemPermissionClient>(services);
        services.AddSingleton<MicrosoftWordContentExtractorClient>();
        services.AddHttpClient<AzureAiSearchPassageAclClient>()
            .RedactLoggedHeaders(["api-key", "Authorization"]);
        services.AddHttpClient<AzureAiSearchPassageSearchClient>()
            .RedactLoggedHeaders(["api-key", "Authorization"]);
        services.AddHttpClient<AzureAiSearchIndexClient>()
            .RedactLoggedHeaders(["api-key", "Authorization"]);
        services.AddHttpClient<OpenAiEmbeddingsClient>()
            .RedactLoggedHeaders(["Authorization"]);
        services.AddScoped<IMicrosoft365ConsentClient, Microsoft365ConsentClientAdapter>();
        services.AddScoped<IMicrosoft365ListItemDeltaClient, Microsoft365ListItemDeltaClientAdapter>();
        services.AddScoped<IMicrosoft365DriveItemDeltaClient, Microsoft365DriveItemDeltaClientAdapter>();
        services.AddScoped<IMicrosoft365DriveContentClient, Microsoft365DriveContentClientAdapter>();
        services.AddScoped<IMicrosoft365ListSchemaClient, Microsoft365ListSchemaClientAdapter>();
        services.AddScoped<IMicrosoft365SiteSourcesClient, Microsoft365SiteSourcesClientAdapter>();
        services.AddScoped<IMicrosoft365SiteClient, Microsoft365SiteClientAdapter>();
        services.AddScoped<IMicrosoft365ApplicationTokenClient, Microsoft365ApplicationTokenClientAdapter>();
        services.AddScoped<IMicrosoft365SubscriptionClient, Microsoft365SubscriptionClientAdapter>();
        services.AddScoped<IMicrosoft365AclResolver, Microsoft365AclResolverAdapter>();
        services.AddScoped<IMicrosoft365UserGroupResolver, Microsoft365UserGroupResolverAdapter>();
        services.AddScoped<IMicrosoft365PassageAclWriter, Microsoft365PassageAclWriterAdapter>();
        services.AddScoped<IMicrosoft365PassageIndexWriter, Microsoft365PassageIndexWriterAdapter>();
        services.AddScoped<IMicrosoft365ContentExtractor, Microsoft365WordContentExtractorAdapter>();
        services.AddScoped<IMicrosoft365EmbeddingGenerator, Microsoft365EmbeddingGeneratorAdapter>();
        services.AddScoped<IMicrosoft365SearchIndexInitializer, Microsoft365SearchIndexInitializerAdapter>();
        services.AddSingleton<IMicrosoft365ClientStateProtector, Microsoft365ClientStateProtectorAdapter>();
        services.AddSingleton(serviceProvider =>
        {
            var serviceBusOptions = serviceProvider.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
            return new AzureServiceBusPublisherClient(serviceBusOptions.FullyQualifiedNamespace);
        });
        services.AddSingleton<Microsoft365SynchronizationPublisherAdapter>();
        services.AddSingleton<Microsoft365LocalSynchronizationPublisherAdapter>();
        services.AddSingleton<IMicrosoft365SynchronizationPublisher>(serviceProvider =>
        {
            var serviceBusOptions = serviceProvider.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
            if (serviceBusOptions.Enabled)
            {
                return serviceProvider.GetRequiredService<Microsoft365SynchronizationPublisherAdapter>();
            }

            return serviceProvider.GetRequiredService<Microsoft365LocalSynchronizationPublisherAdapter>();
        });
        services.AddSingleton<IMicrosoft365ConsentStateProtector, Microsoft365ConsentStateProtectorAdapter>();
        services.AddSingleton<IMicrosoft365TechnicalTokenStore, Microsoft365TechnicalTokenStoreAdapter>();

        return services;
    }

    private static bool IsHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    private static bool IsSecureOrLoopbackUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps
            || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));

    private static bool IsServiceBusNamespace(string value) =>
        Uri.CheckHostName(value) == UriHostNameType.Dns
        && !value.Contains("://", StringComparison.Ordinal);

    private static void AddProtectedHttpClient<TClient>(IServiceCollection services)
        where TClient : class =>
        services.AddHttpClient<TClient>()
            .RedactLoggedHeaders(SensitiveHttpHeaders)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false
            });
}
