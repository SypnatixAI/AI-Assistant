using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using Microsoft.Extensions.AI;

namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

internal sealed class AiModelProviderChatClient(
    IReadOnlyCollection<IAiModelProvider> modelProviders,
    SelectedAiModel selectedModel) : IChatClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public AiModelResponse? LastResponse { get; private set; }

    public int ModelCallCount { get; private set; }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(messages, options);
        var response = await FindSelectedProvider().GetNextActionAsync(
            request,
            cancellationToken);
        RecordResponse(response);

        return CreateChatResponse(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(messages, options);
        var response = await FindSelectedProvider().GetNextActionAsync(
            request,
            cancellationToken);
        RecordResponse(response);

        if (response.Decision.ToolCalls.Count > 0)
        {
            yield return CreateToolCallUpdate(response);
        }
        else
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

    private AiModelRequest CreateRequest(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var orderedMessages = messages.ToArray();
        var currentUserMessage = orderedMessages.LastOrDefault(message => message.Role == ChatRole.User)
            ?? throw new InvalidOperationException("An agent turn requires a user message.");
        var currentUserMessageIndex = Array.LastIndexOf(orderedMessages, currentUserMessage);
        var conversationHistory = orderedMessages
            .TakeWhile(message => !ReferenceEquals(message, currentUserMessage))
            .Select(MapConversationMessage)
            .ToArray();
        var functionMessages = orderedMessages
            .Skip(currentUserMessageIndex + 1)
            .SelectMany(message => message.Contents)
            .ToArray();

        return new AiModelRequest(
            selectedModel,
            CreateStructuredResponseInstructions(
                options?.Instructions,
                options?.Tools?.Count > 0),
            currentUserMessage.Text,
            conversationHistory,
            MapAllowedTools(options?.Tools),
            MapRequestedToolCalls(functionMessages),
            MapToolResults(functionMessages),
            LastResponse?.ContinuationContext);
    }

    private static AiConversationMessage MapConversationMessage(ChatMessage message) =>
        new(
            message.Role == ChatRole.Assistant
                ? AiConversationRole.Assistant
                : AiConversationRole.User,
            message.Text);

    private static string CreateStructuredResponseInstructions(
        string? instructions,
        bool hasEnterpriseSearchTool) =>
        hasEnterpriseSearchTool
            ? $"""
              {instructions}

              Use EnterpriseSearch whenever the user's request is reasonably interpretable as
              a lookup of private, organization-specific, project-specific, or current enterprise
              information. Prefer searching over asking for clarification when the request can
              reasonably be resolved from available enterprise data or conversation history.
              Ask for clarification only when the request remains materially ambiguous and an
              enterprise search would not reasonably help resolve that ambiguity. Answer greetings,
              small talk, and clearly general questions directly
              without calling a tool. After EnterpriseSearch returns evidence, answer only from that
              evidence for enterprise-specific claims and cite only evidenceIds present in the
              tool result.

              Return the final user-facing message through the required structured response schema.
              Use decision "answer" when the available conversation context, general model
              knowledge, or retrieved evidence is enough, "askClarification" when the user must
              provide a missing detail, and "cannotAnswer" when enterprise information would be
              required but cannot be confirmed. Answer in the language of the user's current message.
              """
            : $"""
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

    private static IReadOnlyCollection<AiToolDefinition> MapAllowedTools(
        IList<AITool>? tools) =>
        tools?
            .OfType<AIFunctionDeclaration>()
            .Select(tool => new AiToolDefinition(
                tool.Name,
                tool.Description ?? string.Empty,
                tool.JsonSchema))
            .ToArray()
        ?? [];

    private static IReadOnlyCollection<AiRequestedToolCall> MapRequestedToolCalls(
        IReadOnlyCollection<AIContent> contents) =>
        contents
            .OfType<FunctionCallContent>()
            .Select(toolCall => new AiRequestedToolCall(
                toolCall.CallId,
                toolCall.Name,
                JsonSerializer.SerializeToElement(
                    toolCall.Arguments,
                    SerializerOptions)))
            .ToArray();

    private static IReadOnlyCollection<ToolExecutionResult> MapToolResults(
        IReadOnlyCollection<AIContent> contents) =>
        contents
            .OfType<FunctionResultContent>()
            .Select(MapToolResult)
            .ToArray();

    private static ToolExecutionResult MapToolResult(FunctionResultContent content)
    {
        var resultJson = JsonSerializer.Serialize(content.Result, SerializerOptions);
        var result = JsonSerializer.Deserialize<EnterpriseSearchAgentTool.EnterpriseSearchToolResult>(
            resultJson,
            SerializerOptions)
            ?? throw new InvalidOperationException("The enterprise search tool result is invalid.");

        return result.Status switch
        {
            ToolExecutionStatus.Success => ToolExecutionResult.Succeeded(
                content.CallId,
                result.Evidence),
            ToolExecutionStatus.PartialSuccess => ToolExecutionResult.PartiallySucceeded(
                content.CallId,
                result.Evidence,
                result.Warnings),
            ToolExecutionStatus.Failed => ToolExecutionResult.Failed(
                content.CallId,
                result.ErrorCode ?? ToolExecutionErrorCodes.ExecutorNotFound,
                result.Warnings),
            _ => throw new ArgumentOutOfRangeException(
                nameof(content),
                result.Status,
                "Unsupported tool execution status.")
        };
    }

    private ChatResponse CreateChatResponse(AiModelResponse response) =>
        new(new ChatMessage(ChatRole.Assistant, CreateResponseContents(response)))
        {
            ModelId = selectedModel.ModelName,
            Usage = CreateUsageDetails(response.Usage),
            RawRepresentation = response
        };

    private static ChatResponseUpdate CreateToolCallUpdate(AiModelResponse response) =>
        new(ChatRole.Assistant, CreateToolCallContents(response))
        {
            RawRepresentation = response
        };

    private static IList<AIContent> CreateResponseContents(AiModelResponse response) =>
        response.Decision.ToolCalls.Count > 0
            ? CreateToolCallContents(response)
            : [new TextContent(GetResponseText(response))];

    private static IList<AIContent> CreateToolCallContents(AiModelResponse response) =>
        response.Decision.ToolCalls
            .Select(toolCall => new FunctionCallContent(
                toolCall.CallId,
                toolCall.ToolName,
                MapArguments(toolCall.Arguments)))
            .Cast<AIContent>()
            .ToArray();

    private static IDictionary<string, object?> MapArguments(JsonElement arguments) =>
        JsonSerializer.Deserialize<Dictionary<string, object?>>(
            arguments.GetRawText(),
            SerializerOptions)
        ?? new Dictionary<string, object?>(StringComparer.Ordinal);

    private static string GetResponseText(AiModelResponse response) =>
        string.IsNullOrWhiteSpace(response.Decision.Answer)
            ? response.Decision.Explanation
            : response.Decision.Answer;

    private void RecordResponse(AiModelResponse response)
    {
        LastResponse = response;
        ModelCallCount += response.Usage.ModelCallCount;
    }

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
