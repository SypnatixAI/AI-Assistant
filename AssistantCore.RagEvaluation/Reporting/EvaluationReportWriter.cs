using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.RagEvaluation.Models;

namespace AssistantCore.RagEvaluation.Reporting;

internal sealed class EvaluationReportWriter
{
    public async Task<(string JsonPath, string MarkdownPath)> WriteAsync(
        EvaluationReport report,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "rag-evaluation.json");
        var markdownPath = Path.Combine(outputDirectory, "rag-evaluation.md");
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(report, jsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            markdownPath,
            CreateMarkdown(report),
            cancellationToken);
        return (Path.GetFullPath(jsonPath), Path.GetFullPath(markdownPath));
    }

    internal static string CreateMarkdown(EvaluationReport report)
    {
        var evaluationPassed = report.Summary.Failed == 0;
        var statusIcon = evaluationPassed ? "✅" : "❌";
        var statusLabel = evaluationPassed ? "Passed" : "Failed";
        var builder = new StringBuilder()
            .AppendLine("# RAG evaluation")
            .AppendLine()
            .AppendLine($"> {statusIcon} **{report.Summary.Passed}/{report.Summary.Cases} scenarios passed**")
            .AppendLine()
            .AppendLine("## Summary")
            .AppendLine()
            .AppendLine("| Status | Mode | Model | Passed | Failed | Duration | ACL leaks |")
            .AppendLine("|---|---|---|---:|---:|---:|---:|")
            .AppendLine($"| {statusIcon} {statusLabel} | `{EscapeCell(report.Mode)}` | `{EscapeCell(report.Model)}` | {report.Summary.Passed} | {report.Summary.Failed} | {FormatNumber(report.Summary.DurationMilliseconds)} ms | {report.Summary.AclLeakageCount} |")
            .AppendLine()
            .AppendLine("## Quality metrics")
            .AppendLine()
            .AppendLine("| Retrieval recall | Context precision | Faithfulness | Answer relevance | Citation precision |")
            .AppendLine("|---:|---:|---:|---:|---:|")
            .AppendLine($"| {FormatPercentage(report.Summary.RetrievalRecall)} | {FormatPercentage(report.Summary.ContextPrecision)} | {FormatPercentage(report.Summary.Faithfulness)} | {FormatPercentage(report.Summary.AnswerRelevance)} | {FormatPercentage(report.Summary.CitationPrecision)} |")
            .AppendLine()
            .AppendLine("| Language match | Correct cannot-answer | Correct clarification | ACL leaks |")
            .AppendLine("|---:|---:|---:|---:|")
            .AppendLine($"| {FormatPercentage(report.Summary.LanguageMatchRate)} | {FormatPercentage(report.Summary.CorrectCannotAnswerRate)} | {FormatPercentage(report.Summary.CorrectClarificationRate)} | {report.Summary.AclLeakageCount} |")
            .AppendLine()
            .AppendLine("## Scenario results")
            .AppendLine()
            .AppendLine("| Status | Scenario | Outcome | Recall | Context precision | Faithfulness | Answer relevance | Citation precision |")
            .AppendLine("|:---:|---|---|---:|---:|---:|---:|---:|");

        foreach (var result in report.Results)
        {
            builder.AppendLine(
                $"| {(result.Passed ? "✅" : "❌")} | `{EscapeCell(result.CaseId)}` | {EscapeCell(result.ActualOutcome.ToString())} | {FormatPercentage(result.RetrievalRecall)} | {FormatPercentage(result.ContextPrecision)} | {FormatPercentage(result.Faithfulness)} | {FormatPercentage(result.AnswerRelevance)} | {FormatPercentage(result.CitationPrecision)} |");
        }

        var failedResults = report.Results.Where(result => !result.Passed).ToArray();
        if (failedResults.Length == 0)
        {
            builder.AppendLine().AppendLine("> ✅ Every scenario satisfied its expected behavior and safety checks.");
        }
        else
        {
            builder.AppendLine()
                .AppendLine("<details>")
                .AppendLine($"<summary>Failure details ({failedResults.Length})</summary>")
                .AppendLine();

            foreach (var result in failedResults)
            {
                builder.AppendLine($"### `{EscapeCell(result.CaseId)}`");
                foreach (var failure in result.Failures)
                {
                    builder.AppendLine($"- {failure}");
                }

                if (result.Observation.Error is not null)
                {
                    builder.AppendLine($"- Error: `{result.Observation.Error}`");
                }

                builder.AppendLine();
            }

            builder.AppendLine("</details>");
        }

        return builder
            .AppendLine()
            .AppendLine($"<sub>Generated at {report.GeneratedAtUtc.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC.</sub>")
            .ToString();
    }

    private static string EscapeCell(string value) =>
        value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private static string FormatPercentage(double value) =>
        $"{(value * 100).ToString("0", CultureInfo.InvariantCulture)}%";

    private static string FormatNumber(long value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);
}
