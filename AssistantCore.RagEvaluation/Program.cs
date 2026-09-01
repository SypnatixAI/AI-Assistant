using AssistantCore.RagEvaluation.Configuration;
using AssistantCore.RagEvaluation.Dataset;
using AssistantCore.RagEvaluation.Models;
using AssistantCore.RagEvaluation.Reporting;
using AssistantCore.RagEvaluation.Scoring;
using AssistantCore.RagEvaluation.Targets;

namespace AssistantCore.RagEvaluation;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = RunnerOptions.Parse(args);
            using var cancellationSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationSource.Cancel();
            };

            var dataset = await new EvaluationDatasetLoader().LoadAsync(
                options.DatasetPath,
                cancellationSource.Token);
            var selectedCases = dataset.Cases
                .Where(evaluationCase => evaluationCase.Modes.Contains(
                    options.Mode,
                    StringComparer.OrdinalIgnoreCase))
                .ToArray();
            using var liveProvider = options.Mode == "model"
                ? LiveAiModelProviderScope.Create(options.Model)
                : null;
            var target = new OrchestrationEvaluationTarget(
                (evaluationCase, evidence) => liveProvider?.Provider
                    ?? new ScriptedAiModelProvider(evaluationCase, evidence),
                TimeProvider.System);

            var observations = new List<EvaluationObservation>();
            foreach (var evaluationCase in selectedCases)
            {
                Console.WriteLine($"Running {evaluationCase.Id} ({options.Mode})...");
                observations.Add(await target.RunAsync(
                    evaluationCase,
                    options.Model,
                    cancellationSource.Token));
            }

            var scopedDataset = dataset with { Cases = selectedCases };
            var report = new RagEvaluationScorer().Score(
                scopedDataset,
                observations,
                options.Mode,
                options.Model);
            var paths = await new EvaluationReportWriter().WriteAsync(
                report,
                options.OutputDirectory,
                cancellationSource.Token);

            Console.WriteLine(
                $"RAG evaluation: {report.Summary.Passed}/{report.Summary.Cases} passed.");
            Console.WriteLine($"JSON report: {paths.JsonPath}");
            Console.WriteLine($"Markdown report: {paths.MarkdownPath}");
            return report.Summary.Failed == 0 ? 0 : 1;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"RAG evaluation failed: {exception.Message}");
            return 2;
        }
    }
}
