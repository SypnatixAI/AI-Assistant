using AssistantCore.ExternalServices.Services.OpenAI;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.AgentRuntime;
using AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;
using AssistantCore.Service.Infrastructure.AiModels.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.AiModels.OpenAI;

public sealed class OpenAiAgentChatClientFactory(
    OpenAiResponsesClient responsesClient,
    IOptions<AiModelsOptions> options) : IAgentChatClientFactory
{
    public IChatClient Create(SelectedAiModel selectedModel)
    {
        ArgumentNullException.ThrowIfNull(selectedModel);

        if (!string.Equals(
                selectedModel.Provider,
                OpenAiModelProvider.OpenAiProviderName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Agent chat provider '{selectedModel.Provider}' is not registered.");
        }

        var providerOptions = options.Value.Providers[OpenAiModelProvider.OpenAiProviderName];
        var nativeClient = responsesClient.CreateAgentChatClient(selectedModel.ModelName);

        return new OpenAiAgentChatClient(
            nativeClient,
            TimeSpan.FromSeconds(providerOptions.TimeoutSeconds));
    }
}
