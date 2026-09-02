namespace AssistantCore.RagEvaluation.Models;

public sealed record EvaluationObservation(
    string CaseId,
    EvaluationOutcome Outcome,
    string Answer,
    IReadOnlyCollection<string> RetrievedSourceReferences,
    IReadOnlyCollection<string> CitedSourceReferences,
    IReadOnlyCollection<string> SearchQueries,
    int ModelCalls,
    int ToolCalls,
    long DurationMilliseconds,
    string? Error = null);

public sealed record CaseEvaluationResult(
    string CaseId,
    bool Passed,
    EvaluationOutcome ExpectedOutcome,
    EvaluationOutcome ActualOutcome,
    double RetrievalRecall,
    double ContextPrecision,
    double AnswerRelevance,
    double CitationPrecision,
    double Faithfulness,
    bool LanguageMatch,
    int AclLeakageCount,
    IReadOnlyCollection<string> Failures,
    EvaluationObservation Observation);

public sealed record EvaluationSummary(
    int Cases,
    int Passed,
    int Failed,
    double RetrievalRecall,
    double ContextPrecision,
    double AnswerRelevance,
    double CitationPrecision,
    double Faithfulness,
    double LanguageMatchRate,
    double CorrectCannotAnswerRate,
    double CorrectClarificationRate,
    int AclLeakageCount,
    long DurationMilliseconds);

public sealed record EvaluationReport(
    DateTimeOffset GeneratedAtUtc,
    string Mode,
    string Model,
    EvaluationSummary Summary,
    IReadOnlyCollection<CaseEvaluationResult> Results);
