using AssistantCore.ExternalServices.Entities.Foundry;
using AssistantCore.ExternalServices.Services.Foundry;
using AssistantCore.Service.Application.Services.Messages.AgentRuntime;
using AssistantCore.Service.Infrastructure.Foundry.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Infrastructure.Foundry;

public static class FoundryAgentServiceCollectionExtensions
{
    public static IServiceCollection AddFoundryAgentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<FoundryAgentOptions>()
            .Bind(configuration.GetSection(FoundryAgentOptions.SectionName))
            .Validate(options => options.IsValid(), "Invalid Foundry agent configuration.")
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<FoundryAgentOptions>>()
                .Value;

            return new FoundryAgentExternalClient(
                new FoundryAgentClientSettings(
                    options.ProjectEndpoint,
                    options.AgentName,
                    options.AgentVersion),
                serviceProvider.GetRequiredService<ILogger<FoundryAgentExternalClient>>());
        });

        services.AddSingleton<IFoundryAgentClient, FoundryAgentClientAdapter>();

        return services;
    }
}
