using AssistantCore.RagEvaluation.Models;
using AssistantCore.RagEvaluation.Scoring;

namespace AssistantCore.Service.Tests.RagEvaluation;

public sealed class RagEvaluationScorerTests
{
    [Theory, AutoDomainData]
    public void Given_AFullyGroundedObservation_When_Score_Then_PassesTheCase(Guid _)
    {
        // Given
        var evaluationCase = CreateCase();
        var dataset = new EvaluationDataset(2, "test", [], [evaluationCase]);
        var observation = new EvaluationObservation(
            evaluationCase.Id,
            EvaluationOutcome.Answer,
            "The approved threshold is 5000.",
            ["policy"],
            ["policy"],
            ["approval threshold"],
            2,
            1,
            5);

        // When
        var report = new RagEvaluationScorer().Score(
            dataset,
            [observation],
            "offline",
            "test-model");

        // Then
        var result = Assert.Single(report.Results);
        Assert.True(result.Passed);
        Assert.Equal(1d, result.RetrievalRecall);
        Assert.Equal(1d, result.CitationPrecision);
    }

    [Theory, AutoDomainData]
    public void Given_AnUnauthorizedRetrievedSource_When_Score_Then_FailsAclIsolation(Guid _)
    {
        // Given
        var evaluationCase = CreateCase();
        var dataset = new EvaluationDataset(2, "test", [], [evaluationCase]);
        var observation = new EvaluationObservation(
            evaluationCase.Id,
            EvaluationOutcome.Answer,
            "The approved threshold is 5000.",
            ["policy", "restricted"],
            ["policy"],
            ["approval threshold"],
            2,
            1,
            5);

        // When
        var report = new RagEvaluationScorer().Score(
            dataset,
            [observation],
            "offline",
            "test-model");

        // Then
        var result = Assert.Single(report.Results);
        Assert.False(result.Passed);
        Assert.Equal(1, result.AclLeakageCount);
        Assert.Equal(1, report.Summary.AclLeakageCount);
    }

    private static RagEvaluationCase CreateCase() => new(
        "enterprise-threshold",
        "en",
        ["What is the approved threshold?"],
        ToolsAvailable: true,
        Modes: ["offline"],
        Documents:
        [
            new EvaluationDocument("policy", "Policy", "The threshold is 5000."),
            new EvaluationDocument("restricted", "Restricted", "Secret.", Allowed: false)
        ],
        new EvaluationExpectation(
            EvaluationOutcome.Answer,
            ["5000"],
            [],
            ["policy"],
            ["restricted"],
            MinimumSearchRounds: 1),
        new EvaluationFixture(
            EvaluationOutcome.Answer,
            "The approved threshold is 5000.",
            [["policy"]],
            ["policy"],
            ["approval threshold"]));
}
