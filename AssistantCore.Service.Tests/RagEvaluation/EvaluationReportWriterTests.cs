using AssistantCore.RagEvaluation.Models;
using AssistantCore.RagEvaluation.Reporting;

namespace AssistantCore.Service.Tests.RagEvaluation;

public sealed class EvaluationReportWriterTests
{
    [Theory, AutoDomainData]
    public void Given_ACompletedEvaluation_When_CreateMarkdown_Then_RendersTheImportantResults(
        DateTimeOffset generatedAtUtc)
    {
        // Given
        var observation = new EvaluationObservation(
            "enterprise-policy",
            EvaluationOutcome.Answer,
            "The policy is available.",
            ["policy"],
            ["policy"],
            ["enterprise policy"],
            2,
            1,
            125);
        var result = new CaseEvaluationResult(
            "enterprise-policy",
            true,
            EvaluationOutcome.Answer,
            EvaluationOutcome.Answer,
            1d,
            0.9d,
            0.8d,
            1d,
            1d,
            true,
            0,
            [],
            observation);
        var summary = new EvaluationSummary(
            1,
            1,
            0,
            1d,
            0.9d,
            0.8d,
            1d,
            1d,
            1d,
            1d,
            1d,
            0,
            125);
        var report = new EvaluationReport(
            generatedAtUtc,
            "offline",
            "test-model",
            summary,
            [result]);

        // When
        var markdown = EvaluationReportWriter.CreateMarkdown(report);

        // Then
        Assert.Contains("✅ **1/1 scenarios passed**", markdown, StringComparison.Ordinal);
        Assert.Contains("## Quality metrics", markdown, StringComparison.Ordinal);
        Assert.Contains("| 100% | 90% | 100% | 80% | 100% |", markdown, StringComparison.Ordinal);
        Assert.Contains("`enterprise-policy`", markdown, StringComparison.Ordinal);
        Assert.Contains("Every scenario satisfied", markdown, StringComparison.Ordinal);
    }
}
