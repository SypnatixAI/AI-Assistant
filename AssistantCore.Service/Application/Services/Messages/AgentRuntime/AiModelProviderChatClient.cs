using System.Threading.Channels;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using Microsoft.Extensions.AI;

namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

internal sealed class AiModelProviderChatClient(
    IReadOnlyCollection<IAiModelProvider> modelProviders,
    SelectedAiModel selectedModel) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(messages, options);
        var response = await FindSelectedProvider().GetNextActionAsync(
            request,
            cancellationToken);

        return CreateChatResponse(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(messages, options);
        var updates = Channel.CreateUnbounded<ChatResponseUpdate>();
        var streamedAnyText = false;
        var providerTask = StreamProviderResponseAsync(
            request,
            updates.Writer,
            () => streamedAnyText = true,
            cancellationToken);

        await foreach (var update in updates.Reader.ReadAllAsync(cancellationToken))
        {
            yield return update;
        }

        var response = await providerTask;
        if (!streamedAnyText)
        {
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                GetResponseText(response))
            {
                ModelId = selectedModel.ModelName,
                RawRepresentation = response
            };
        }

        yield return new ChatResponseUpdate
        {
            Contents = [new UsageContent(CreateUsageDetails(response.Usage))],
            ModelId = selectedModel.ModelName,
            RawRepresentation = response
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceKey is null && serviceType.IsInstanceOfType(this)
            ? this
            : null;
    }

    public void Dispose()
    {
    }

    private async Task<AiModelResponse> StreamProviderResponseAsync(
        AiModelRequest request,
        ChannelWriter<ChatResponseUpdate> writer,
        Action onTextStreamed,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await FindSelectedProvider().GetNextActionStreamingAsync(
                request,
                async (delta, token) =>
                {
                    if (string.IsNullOrEmpty(delta))
                    {
                        return;
                    }

                    onTextStreamed();
                    await writer.WriteAsync(
                        new ChatResponseUpdate(ChatRole.Assistant, delta)
                        {
                            ModelId = selectedModel.ModelName
                        },
                        token);
                },
                cancellationToken);

            writer.TryComplete();
            return response;
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
            throw;
        }
    }

    private AiModelRequest CreateRequest(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var orderedMessages = messages.ToArray();
        var currentUserMessage = orderedMessages.LastOrDefault(message => message.Role == ChatRole.User)
            ?? throw new InvalidOperationException("An agent turn requires a user message.");
        var conversationHistory = orderedMessages
            .TakeWhile(message => !ReferenceEquals(message, currentUserMessage))
            .Select(MapConversationMessage)
            .ToArray();

        return new AiModelRequest(
            selectedModel,
            CreateStructuredResponseInstructions(options?.Instructions),
            currentUserMessage.Text,
            conversationHistory,
            AllowedTools: [],
            RequestedToolCalls: [],
            ToolResults: []);
    }

    private static AiConversationMessage MapConversationMessage(ChatMessage message) =>
        new(
            message.Role == ChatRole.Assistant
                ? AiConversationRole.Assistant
                : AiConversationRole.User,
            message.Text);

    private static string CreateStructuredResponseInstructions(string? instructions) =>
        $"""
        {instructions}

        No tool or enterprise retrieval capability is available in this runtime step.
        If the user asks for private, organization-specific, project-specific, or current
        enterprise information, explain that the information cannot be confirmed from the
        available context. Answer in the language of the user's current message.

        Return the final user-facing message through the required structured response schema.
        Use decision "answer" when the available conversation context or general model
        knowledge is enough, "askClarification" when the user must provide a missing detail,
        and "cannotAnswer" when enterprise information would be required. Use an empty
        evidenceIds array because retrieval is not connected in this step.
        """;

    private ChatResponse CreateChatResponse(AiModelResponse response) =>
        new(new ChatMessage(ChatRole.Assistant, GetResponseText(response)))
        {
            ModelId = selectedModel.ModelName,
            Usage = CreateUsageDetails(response.Usage),
            RawRepresentation = response
        };

    private static string GetResponseText(AiModelResponse response) =>
        string.IsNullOrWhiteSpace(response.Decision.Answer)
            ? response.Decision.Explanation
            : response.Decision.Answer;

    private static UsageDetails CreateUsageDetails(AiModelUsage usage) =>
        new()
        {
            InputTokenCount = usage.InputTokens,
            OutputTokenCount = usage.OutputTokens,
            TotalTokenCount = usage.InputTokens + usage.OutputTokens
        };

    private IAiModelProvider FindSelectedProvider()
    {
        var matchingProviders = modelProviders
            .Where(provider => string.Equals(
                provider.ProviderName,
                selectedModel.Provider,
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (matchingProviders.Length != 1)
        {
            throw new InvalidOperationException(
                "The selected AI model provider is not uniquely registered.");
        }

        return matchingProviders[0];
    }
}
