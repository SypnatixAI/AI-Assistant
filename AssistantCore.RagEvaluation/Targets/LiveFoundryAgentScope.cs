using AssistantCore.Service.Application.Services.Messages.AgentRuntime;
using AssistantCore.Service.Infrastructure.Foundry;
using AssistantCore.Service.Infrastructure.Foundry.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AssistantCore.RagEvaluation.Targets;

internal sealed class LiveFoundryAgentScope : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    private LiveFoundryAgentScope(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Client = serviceProvider.GetRequiredService<IFoundryAgentClient>();
        var options = serviceProvider.GetRequiredService<IOptions<FoundryAgentOptions>>().Value;
        AgentIdentifier = $"{options.AgentName}:{options.AgentVersion}";
    }

    public IFoundryAgentClient Client { get; }

    public string AgentIdentifier { get; }

    public static LiveFoundryAgentScope Create()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("AssistantCore.Service/appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFoundryAgentInfrastructure(configuration);
        return new LiveFoundryAgentScope(services.BuildServiceProvider());
    }

    public void Dispose() => _serviceProvider.Dispose();
}
