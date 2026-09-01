using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Infrastructure.AiModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.RagEvaluation.Targets;

internal sealed class LiveAiModelProviderScope : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    private LiveAiModelProviderScope(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Provider = serviceProvider.GetRequiredService<IAiModelProvider>();
    }

    public IAiModelProvider Provider { get; }

    public static LiveAiModelProviderScope Create(string model)
    {
        var apiKey = Environment.GetEnvironmentVariable("RAG_EVAL_OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "RAG_EVAL_OPENAI_API_KEY is required only for --mode model.");
        }

        var endpoint = Environment.GetEnvironmentVariable("RAG_EVAL_OPENAI_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "https://api.openai.com/v1";
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiModels:DefaultModel"] = model,
                ["AiModels:Providers:OpenAI:Enabled"] = "true",
                ["AiModels:Providers:OpenAI:Endpoint"] = endpoint,
                ["AiModels:Providers:OpenAI:ApiKey"] = apiKey,
                ["AiModels:Providers:OpenAI:TimeoutSeconds"] = "120",
                [$"AiModels:Providers:OpenAI:Models:{model}:Enabled"] = "true"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddAiModelInfrastructure(configuration);
        return new LiveAiModelProviderScope(services.BuildServiceProvider());
    }

    public void Dispose() => _serviceProvider.Dispose();
}
