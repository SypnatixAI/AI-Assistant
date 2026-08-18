using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Evidence;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public sealed class OrchestrationResultBuilder(
    IEvidenceCitationResolver citationResolver) : IOrchestrationResultBuilder
{
    public const string InsufficientInformationAnswer =
        "The available information is insufficient to answer with confidence.";

    public MessageOrchestrationResult Build(
        MessageOrchestrationState state,
        AiModelResponse finalResponse)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(finalResponse);

        ThrowWhenAllSourcesFailed(state);

        var answer = BuildAnswer(state, finalResponse.Decision);
        var citedEvidence = citationResolver.Resolve(
            finalResponse.Decision.CitedEvidenceIds,
            state.CollectedEvidence);

        return new MessageOrchestrationResult(
            answer,
            state.SelectedModel.ModelName,
            citedEvidence,
            state.Warnings,
            state.Budget.Usage);
    }

    private static string BuildAnswer(
        MessageOrchestrationState state,
        AiModelDecision decision) =>
        decision.Type switch
        {
            AiModelDecisionType.Answer
                when !string.IsNullOrWhiteSpace(decision.Answer) =>
                decision.Answer.Trim(),
            AiModelDecisionType.InsufficientInformation => InsufficientInformationAnswer,
            _ => throw new AiProviderInvalidResponseException(
                state.SelectedModel.Provider)
        };

    private static void ThrowWhenAllSourcesFailed(MessageOrchestrationState state)
    {
        var toolResults = state.ToolResults;
        if (toolResults.Count > 0
            && toolResults.All(result => result.Status == ToolExecutionStatus.Failed)
            && state.CollectedEvidence.Count == 0)
        {
            throw new ExternalSourcesUnavailableException();
        }
    }
}
