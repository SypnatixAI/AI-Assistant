using System.Text.RegularExpressions;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public sealed partial class OrchestrationResultBuilder(
    IEvidenceCitationResolver citationResolver,
    ILogger<OrchestrationResultBuilder> logger) : IOrchestrationResultBuilder
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

        var sanitizedAnswer = SanitizeAnswer(state, answer);

        return !string.IsNullOrWhiteSpace(sanitizedAnswer)
            ? sanitizedAnswer
            : throw new AiProviderInvalidResponseException(state.SelectedModel.Provider);
    }

    private static string SanitizeAnswer(MessageOrchestrationState state, string answer)
    {
        var sanitizedAnswer = EvidenceIdentifierPattern()
            .Replace(answer, string.Empty);

        if (LatinLetterPattern().IsMatch(sanitizedAnswer))
        {
            sanitizedAnswer = UnexpectedScriptWordPattern()
                .Replace(sanitizedAnswer, match =>
                    IsSupportedNonLatinWord(state, match.Groups["word"].Value)
                        ? match.Value
                        : string.Empty);
        }

        return RepeatedWhitespacePattern()
            .Replace(
                SpaceBeforePunctuationPattern()
                    .Replace(sanitizedAnswer, "$1"),
                " ")
            .Trim();
    }

    private static bool IsSupportedNonLatinWord(MessageOrchestrationState state, string word)
    {
        if (state.Question.Contains(word, StringComparison.Ordinal))
        {
            return true;
        }

        return state.CollectedEvidence.Any(evidence =>
            evidence.Title.Contains(word, StringComparison.Ordinal)
            || evidence.Content.Contains(word, StringComparison.Ordinal)
            || evidence.Reference.Contains(word, StringComparison.Ordinal)
            || (evidence.Url?.Contains(word, StringComparison.Ordinal) ?? false));
    }

    [GeneratedRegex(
        @"[ \t]*\[?evidence-[a-f0-9]{24}\]?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EvidenceIdentifierPattern();

    [GeneratedRegex(
        @"[A-Za-z\u00C0-\u024F]",
        RegexOptions.CultureInvariant)]
    private static partial Regex LatinLetterPattern();

    [GeneratedRegex(
        @"[ \t]+(?<word>[\p{IsHebrew}\p{IsArabic}\p{IsCyrillic}]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex UnexpectedScriptWordPattern();

    [GeneratedRegex(
        @"[ \t]+([,.;:!?])",
        RegexOptions.CultureInvariant)]
    private static partial Regex SpaceBeforePunctuationPattern();

    [GeneratedRegex(
        @"[ \t]{2,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedWhitespacePattern();

    private void ThrowWhenAnyCitationIsUnknown(
        MessageOrchestrationState state,
        AiModelDecision decision,
        IReadOnlyCollection<RetrievedEvidence> citedEvidence)
    {
        var requestedEvidenceIds = decision.CitedEvidenceIds.ToArray();
        var distinctRequestedEvidenceIds = requestedEvidenceIds
            .Where(evidenceId => !string.IsNullOrWhiteSpace(evidenceId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (distinctRequestedEvidenceIds.Length != requestedEvidenceIds.Length
            || citedEvidence.Count != distinctRequestedEvidenceIds.Length)
        {
            var availableEvidenceIds = state.CollectedEvidence
                .Select(evidence => evidence.EvidenceId)
                .ToArray();
            logger.LogWarning(
                "AI provider returned invalid evidence citations. Requested evidenceIds: {RequestedEvidenceIds}. Available evidenceIds: {AvailableEvidenceIds}.",
                requestedEvidenceIds,
                availableEvidenceIds);

            if (state.CitationRepairResponseRequired && citedEvidence.Count > 0)
            {
                return;
            }

            throw new AiProviderInvalidCitationResponseException(state.SelectedModel.Provider);
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
