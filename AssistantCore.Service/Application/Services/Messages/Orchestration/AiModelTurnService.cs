using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.AiModels;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public sealed class AiModelTurnService(
    IEnumerable<IAiModelProvider> modelProviders,
    TimeProvider timeProvider) : IAiModelTurnService
{
    private const string OrchestrationInstructions =
        """
        Decide whether the current question can be answered directly, requires one or more
        of the available read-only tools, or cannot be answered with the available information.
        Use only tools included in the request. When tools are required, request every useful
        tool call without inventing results. Otherwise return a structured decision named
        answer or cannotAnswer, with a clear reason, an answer, and evidenceIds. Do not claim
        that enterprise information was found unless it is supported by supplied evidence.
        """;

    public async Task<AiModelResponse> RequestNextActionAsync(
        MessageOrchestrationState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();

        var provider = FindSelectedProvider(state.SelectedModel.Provider);
        var request = new AiModelRequest(
            state.SelectedModel,
            OrchestrationInstructions,
            state.Question,
            state.ConversationHistory,
            state.AllowedTools,
            state.RequestedToolCalls,
            state.ToolResults,
            state.ContinuationContext);

        var response = await provider.GetNextActionAsync(request, cancellationToken);
        state.RecordModelResponse(response, timeProvider.GetUtcNow());

        return response;
    }

    private IAiModelProvider FindSelectedProvider(string providerName)
    {
        var matchingProviders = modelProviders
            .Where(provider => string.Equals(
                provider.ProviderName,
                providerName,
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
