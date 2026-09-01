using AssistantCore.RagEvaluation.Models;

namespace AssistantCore.RagEvaluation.Scoring;

public sealed class RagEvaluationScorer
{
    public EvaluationReport Score(
        EvaluationDataset dataset,
        IReadOnlyCollection<EvaluationObservation> observations,
        string mode,
        string model)
    {
        var observationsByCase = observations.ToDictionary(item => item.CaseId, StringComparer.Ordinal);
        var results = dataset.Cases
            .Where(evaluationCase => evaluationCase.Modes.Contains(mode, StringComparer.OrdinalIgnoreCase))
            .Select(evaluationCase => ScoreCase(evaluationCase, observationsByCase[evaluationCase.Id]))
            .ToArray();

        return new EvaluationReport(
            DateTimeOffset.UtcNow,
            mode,
            model,
            CreateSummary(results),
            results);
    }

    private static CaseEvaluationResult ScoreCase(
        RagEvaluationCase evaluationCase,
        EvaluationObservation observation)
    {
        var expected = evaluationCase.Expected;
        var expectedSources = expected.ExpectedSourceReferences.ToHashSet(StringComparer.Ordinal);
        var allowedSources = evaluationCase.Documents
            .Where(document => document.Allowed)
            .Select(document => document.Reference)
            .ToHashSet(StringComparer.Ordinal);
        var forbiddenSources = expected.ForbiddenSourceReferences.ToHashSet(StringComparer.Ordinal);
        var retrieved = observation.RetrievedSourceReferences.ToHashSet(StringComparer.Ordinal);
        var cited = observation.CitedSourceReferences.ToHashSet(StringComparer.Ordinal);

        var retrievalRecall = Ratio(expectedSources.Count(source => retrieved.Contains(source)), expectedSources.Count);
        var contextPrecision = Ratio(retrieved.Count(source => allowedSources.Contains(source)), retrieved.Count);
        var answerRelevance = Ratio(
            expected.RequiredAnswerTerms.Count(term => Contains(observation.Answer, term)),
            expected.RequiredAnswerTerms.Count);
        var citationPrecision = Ratio(cited.Count(source => expectedSources.Contains(source)), cited.Count);
        var forbiddenTermsAbsent = expected.ForbiddenAnswerTerms.All(term => !Contains(observation.Answer, term));
        var aclLeakageCount = retrieved.Concat(cited).Distinct(StringComparer.Ordinal)
            .Count(forbiddenSources.Contains);
        var faithfulness = cited.All(retrieved.Contains) && forbiddenTermsAbsent && aclLeakageCount == 0
            ? 1d
            : 0d;
        var languageMatch = expected.Outcome is EvaluationOutcome.Rejected or EvaluationOutcome.Error
            || MatchesLanguage(evaluationCase.Language, observation.Answer);

        var failures = new List<string>();
        AddFailure(observation.Outcome == expected.Outcome, "Unexpected terminal outcome.", failures);
        AddFailure(retrievalRecall == 1d, "Expected evidence was not retrieved.", failures);
        AddFailure(contextPrecision == 1d, "Retrieved context contains an unauthorized or irrelevant source.", failures);
        AddFailure(answerRelevance == 1d, "The answer is missing an expected fact.", failures);
        AddFailure(citationPrecision == 1d, "A citation does not match an expected source.", failures);
        AddFailure(faithfulness == 1d, "The answer or its citations are not fully grounded.", failures);
        AddFailure(languageMatch, "The answer language does not match the request.", failures);
        AddFailure(observation.SearchQueries.Count >= expected.MinimumSearchRounds,
            "The minimum number of retrieval rounds was not reached.", failures);
        AddFailure(aclLeakageCount == 0, "Unauthorized evidence leaked into the result.", failures);

        return new CaseEvaluationResult(
            evaluationCase.Id,
            failures.Count == 0,
            expected.Outcome,
            observation.Outcome,
            retrievalRecall,
            contextPrecision,
            answerRelevance,
            citationPrecision,
            faithfulness,
            languageMatch,
            aclLeakageCount,
            failures,
            observation);
    }

    private static EvaluationSummary CreateSummary(IReadOnlyCollection<CaseEvaluationResult> results) =>
        new(
            results.Count,
            results.Count(result => result.Passed),
            results.Count(result => !result.Passed),
            Average(results, result => result.RetrievalRecall),
            Average(results, result => result.ContextPrecision),
            Average(results, result => result.AnswerRelevance),
            Average(results, result => result.CitationPrecision),
            Average(results, result => result.Faithfulness),
            Average(results, result => result.LanguageMatch ? 1d : 0d),
            OutcomeRate(results, EvaluationOutcome.CannotAnswer),
            OutcomeRate(results, EvaluationOutcome.Clarify),
            results.Sum(result => result.AclLeakageCount),
            results.Sum(result => result.Observation.DurationMilliseconds));

    private static double Ratio(int numerator, int denominator) =>
        denominator == 0 ? 1d : (double)numerator / denominator;

    private static double Average(
        IReadOnlyCollection<CaseEvaluationResult> results,
        Func<CaseEvaluationResult, double> selector) =>
        results.Count == 0 ? 0d : results.Average(selector);

    private static double OutcomeRate(
        IReadOnlyCollection<CaseEvaluationResult> results,
        EvaluationOutcome outcome)
    {
        var matching = results.Where(result => result.ExpectedOutcome == outcome).ToArray();
        return matching.Length == 0
            ? 1d
            : matching.Count(result => result.ActualOutcome == outcome) / (double)matching.Length;
    }

    private static bool Contains(string answer, string term) =>
        answer.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesLanguage(string language, string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        var normalized = $" {answer.ToLowerInvariant()} ";
        var frenchSignals = new[] { " le ", " la ", " les ", " une ", " est ", " pas ", " quel ", " pouvez " };
        var hasFrenchSignal = frenchSignals.Any(normalized.Contains);
        return string.Equals(language, "fr", StringComparison.OrdinalIgnoreCase)
            ? hasFrenchSignal
            : !hasFrenchSignal;
    }

    private static void AddFailure(bool condition, string message, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
}
