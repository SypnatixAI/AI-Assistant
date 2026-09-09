using System.Text.Json;
using AssistantCore.ExternalServices.Entities.Foundry;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AssistantCore.ExternalServices.Services.Foundry;

public sealed class FoundryAgentExternalClient
{
    private readonly FoundryAgentClientSettings _settings;
    private readonly AIProjectClient _projectClient;
    private readonly ILogger<FoundryAgentExternalClient> _logger;

    public FoundryAgentExternalClient(
        FoundryAgentClientSettings settings,
        ILogger<FoundryAgentExternalClient> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _logger = logger;
        _projectClient = new AIProjectClient(
            new Uri(settings.ProjectEndpoint),
            new DefaultAzureCredential());
    }

    public async Task<FoundryAgentExternalResult> RunAsync(
        FoundryAgentExternalRequest request,
        Func<FoundryAgentExternalToolCall, CancellationToken, Task<string>> toolExecutor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(toolExecutor);

        var agent = CreateAgent(request.Tools, toolExecutor);
        var response = await agent.RunAsync(
            CreateMessages(request),
            cancellationToken: cancellationToken);

        return new FoundryAgentExternalResult(
            response.Text,
            CreateAgentIdentifier(),
            ToTokenCount(response.Usage?.InputTokenCount),
            ToTokenCount(response.Usage?.OutputTokenCount),
            ModelCallCount: 1);
    }

    public async Task<FoundryAgentExternalResult> RunStreamingAsync(
        FoundryAgentExternalRequest request,
        Func<FoundryAgentExternalToolCall, CancellationToken, Task<string>> toolExecutor,
        Func<string, CancellationToken, ValueTask> onAnswerDelta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(toolExecutor);
        ArgumentNullException.ThrowIfNull(onAnswerDelta);

        var agent = CreateAgent(request.Tools, toolExecutor);
        var responseText = new List<string>();
        UsageDetails? usage = null;

        await foreach (var update in agent.RunStreamingAsync(
                           CreateMessages(request),
                           cancellationToken: cancellationToken))
        {
            usage = update.Contents
                .OfType<UsageContent>()
                .LastOrDefault()
                ?.Details
                ?? usage;

            if (string.IsNullOrWhiteSpace(update.Text)
                || update.Contents.OfType<FunctionCallContent>().Any()
                || update.Contents.OfType<FunctionResultContent>().Any())
            {
                continue;
            }

            responseText.Add(update.Text);
            await onAnswerDelta(update.Text, cancellationToken);
        }

        return new FoundryAgentExternalResult(
            string.Concat(responseText),
            CreateAgentIdentifier(),
            ToTokenCount(usage?.InputTokenCount),
            ToTokenCount(usage?.OutputTokenCount),
            ModelCallCount: 1);
    }

    private AIAgent CreateAgent(
        IReadOnlyCollection<FoundryAgentExternalToolDefinition> toolDefinitions,
        Func<FoundryAgentExternalToolCall, CancellationToken, Task<string>> toolExecutor)
    {
        var tools = toolDefinitions
            .Select(definition => CreateTool(definition, toolExecutor))
            .Cast<AITool>()
            .ToArray();

        _logger.LogInformation(
            "Using Foundry agent {AgentName} version {AgentVersion} from {ProjectEndpoint}.",
            _settings.AgentName,
            _settings.AgentVersion,
            _settings.ProjectEndpoint);

        return _projectClient.AsAIAgent(
            new AgentReference(
                _settings.AgentName,
                _settings.AgentVersion),
            tools: tools);
    }

    private static AIFunction CreateTool(
        FoundryAgentExternalToolDefinition definition,
        Func<FoundryAgentExternalToolCall, CancellationToken, Task<string>> toolExecutor)
    {
        async Task<string> InvokeAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            var values = arguments.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal);
            var serializedArguments = JsonSerializer.SerializeToElement(values);
            return await toolExecutor(
                new FoundryAgentExternalToolCall(
                    definition.Name,
                    serializedArguments),
                cancellationToken);
        }

        var function = AIFunctionFactory.Create(
            (Func<AIFunctionArguments, CancellationToken, Task<string>>)InvokeAsync,
            new AIFunctionFactoryOptions
            {
                Name = definition.Name,
                Description = definition.Description
            });

        return new JsonSchemaFunction(function, definition.InputSchema);
    }

    private static IReadOnlyCollection<ChatMessage> CreateMessages(
        FoundryAgentExternalRequest request)
    {
        var messages = request.ConversationHistory
            .Select(message => new ChatMessage(
                message.Role == FoundryAgentExternalMessageRole.Assistant
                    ? ChatRole.Assistant
                    : ChatRole.User,
                message.Content))
            .ToList();

        messages.Add(new ChatMessage(ChatRole.User, request.UserMessage));
        return messages;
    }

    private string CreateAgentIdentifier() =>
        $"{_settings.AgentName}@{_settings.AgentVersion}";

    private static int ToTokenCount(long? tokenCount) =>
        tokenCount is null ? 0 : checked((int)tokenCount.Value);

    private sealed class JsonSchemaFunction(
        AIFunction innerFunction,
        JsonElement inputSchema) : DelegatingAIFunction(innerFunction)
    {
        public override JsonElement JsonSchema => inputSchema;
    }
}
