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

    private static string CreateMarkdown(EvaluationReport report)
    {
        var builder = new StringBuilder()
            .AppendLine("# RAG evaluation report")
            .AppendLine()
            .AppendLine($"- Mode: `{report.Mode}`")
            .AppendLine($"- Model: `{report.Model}`")
            .AppendLine($"- Result: {report.Summary.Passed}/{report.Summary.Cases} cases passed")
            .AppendLine($"- ACL leakage: {report.Summary.AclLeakageCount}")
            .AppendLine()
            .AppendLine("| Case | Passed | Outcome | Recall | Context precision | Faithfulness | Answer relevance | Citation precision |")
            .AppendLine("|---|---:|---|---:|---:|---:|---:|---:|");

        foreach (var result in report.Results)
        {
            builder.AppendLine(
                $"| {result.CaseId} | {(result.Passed ? "yes" : "no")} | {result.ActualOutcome} | {result.RetrievalRecall:P0} | {result.ContextPrecision:P0} | {result.Faithfulness:P0} | {result.AnswerRelevance:P0} | {result.CitationPrecision:P0} |");
        }

        foreach (var result in report.Results.Where(result => !result.Passed))
        {
            builder.AppendLine().AppendLine($"## {result.CaseId}");
            foreach (var failure in result.Failures)
            {
                builder.AppendLine($"- {failure}");
            }
            if (result.Observation.Error is not null)
            {
                builder.AppendLine($"- Error: `{result.Observation.Error}`");
            }
        }

        return builder.ToString();
    }
}
