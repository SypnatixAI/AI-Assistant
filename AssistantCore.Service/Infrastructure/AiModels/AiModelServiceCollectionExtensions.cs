using AssistantCore.ExternalServices.Entities.OpenAI.Models;
using AssistantCore.ExternalServices.Services.OpenAI;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;
using AssistantCore.Service.Infrastructure.AiModels.Configuration;
using AssistantCore.Service.Infrastructure.AiModels.OpenAI;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.AiModels;

public static class AiModelServiceCollectionExtensions
{
    public static IServiceCollection AddAiModelInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<AiModelsOptions>, AiModelsOptionsValidator>();
        services.AddOptions<AiModelsOptions>()
            .Bind(configuration.GetSection(AiModelsOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IAuthorizedAiModelSelector, AuthorizedAiModelSelector>();
        services.AddSingleton(serviceProvider =>
        {
            var aiModelsOptions = serviceProvider
                .GetRequiredService<IOptions<AiModelsOptions>>()
                .Value;
            var openAiOptions = aiModelsOptions.Providers[OpenAiModelProvider.OpenAiProviderName];

            return new OpenAiResponsesClient(
                new OpenAiClientSettings(openAiOptions.Endpoint, openAiOptions.ApiKey));
        });
        services.AddSingleton<OpenAiResponsesRequestAdapter>();
        services.AddSingleton<IOpenAiResponsesClient, OpenAiResponsesClientAdapter>();
        services.AddSingleton<OpenAiResponseMapper>();
        services.AddSingleton<IAiModelProvider>(serviceProvider =>
        {
            var aiModelsOptions = serviceProvider
                .GetRequiredService<IOptions<AiModelsOptions>>()
                .Value;
            var timeoutSeconds = aiModelsOptions
                .Providers[OpenAiModelProvider.OpenAiProviderName]
                .TimeoutSeconds;

            return new OpenAiModelProvider(
                serviceProvider.GetRequiredService<IOpenAiResponsesClient>(),
                serviceProvider.GetRequiredService<OpenAiResponseMapper>(),
                TimeSpan.FromSeconds(timeoutSeconds));
        });

        return services;
    }
}
