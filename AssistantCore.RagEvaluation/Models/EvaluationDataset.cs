using System.Text.Json.Serialization;

namespace AssistantCore.RagEvaluation.Models;

public sealed record EvaluationDataset(
    int Version,
    string Scope,
    IReadOnlyCollection<string> Dimensions,
    IReadOnlyCollection<RagEvaluationCase> Cases);

public sealed record RagEvaluationCase(
    string Id,
    string Language,
    IReadOnlyCollection<string> Conversation,
    bool ToolsAvailable,
    IReadOnlyCollection<string> Modes,
    IReadOnlyCollection<EvaluationDocument> Documents,
    EvaluationExpectation Expected,
    EvaluationFixture Fixture);

public sealed record EvaluationDocument(
    string Reference,
    string Title,
    string Content,
    bool Allowed = true);

public sealed record EvaluationExpectation(
    EvaluationOutcome Outcome,
    IReadOnlyCollection<string> RequiredAnswerTerms,
    IReadOnlyCollection<string> ForbiddenAnswerTerms,
    IReadOnlyCollection<string> ExpectedSourceReferences,
    IReadOnlyCollection<string> ForbiddenSourceReferences,
    int MinimumSearchRounds = 0);

public sealed record EvaluationFixture(
    EvaluationOutcome Outcome,
    string Answer,
    IReadOnlyCollection<IReadOnlyCollection<string>> RetrievalRounds,
    IReadOnlyCollection<string> CitedSourceReferences,
    IReadOnlyCollection<string> SearchQueries);

[JsonConverter(typeof(JsonStringEnumConverter<EvaluationOutcome>))]
public enum EvaluationOutcome
{
    Answer,
    Clarify,
    CannotAnswer,
    Rejected,
    Error
}
