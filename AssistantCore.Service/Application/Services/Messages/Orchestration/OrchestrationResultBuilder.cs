using System.Text.RegularExpressions;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Evidence;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public sealed partial class OrchestrationResultBuilder(
    IEvidenceCitationResolver citationResolver) : IOrchestrationResultBuilder
{
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
        ThrowWhenAnyCitationIsUnknown(state, finalResponse.Decision, citedEvidence);
        ThrowWhenGroundedAnswerHasNoCitation(state, finalResponse.Decision, citedEvidence);

        return new MessageOrchestrationResult(
            answer,
            state.SelectedModel.ModelName,
            citedEvidence,
            state.Warnings,
            state.Budget.Usage);
    }

    private static string BuildAnswer(
        MessageOrchestrationState state,
        AiModelDecision decision)
    {
        var answer = decision.Type switch
        {
            AiModelDecisionType.Answer or
            AiModelDecisionType.AskClarification or
            AiModelDecisionType.InsufficientInformation
                when !string.IsNullOrWhiteSpace(decision.Answer) =>
                decision.Answer.Trim(),
            _ => throw new AiProviderInvalidResponseException(
                state.SelectedModel.Provider)
        };

        var sanitizedAnswer = EvidenceIdentifierPattern()
            .Replace(answer, string.Empty)
            .Trim();

        return !string.IsNullOrWhiteSpace(sanitizedAnswer)
            ? sanitizedAnswer
            : throw new AiProviderInvalidResponseException(state.SelectedModel.Provider);
    }

    [GeneratedRegex(
        @"[ \t]*\[?evidence-[a-f0-9]{24}\]?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EvidenceIdentifierPattern();

    private static void ThrowWhenAnyCitationIsUnknown(
        MessageOrchestrationState state,
        AiModelDecision decision,
        IReadOnlyCollection<RetrievedEvidence> citedEvidence)
    {
        var requestedEvidenceIds = decision.CitedEvidenceIds
            .Where(evidenceId => !string.IsNullOrWhiteSpace(evidenceId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (requestedEvidenceIds.Length != decision.CitedEvidenceIds.Count
            || citedEvidence.Count != requestedEvidenceIds.Length)
        {
            throw new AiProviderInvalidResponseException(state.SelectedModel.Provider);
        }
    }

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

    private static void ThrowWhenGroundedAnswerHasNoCitation(
        MessageOrchestrationState state,
        AiModelDecision decision,
        IReadOnlyCollection<RetrievedEvidence> citedEvidence)
    {
        if (decision.Type == AiModelDecisionType.Answer
            && state.CollectedEvidence.Count > 0
            && citedEvidence.Count == 0)
        {
            throw new AiProviderInvalidResponseException(state.SelectedModel.Provider);
        }
    }
}
