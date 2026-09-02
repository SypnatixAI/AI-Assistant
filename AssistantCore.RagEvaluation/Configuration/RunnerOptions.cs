namespace AssistantCore.RagEvaluation.Configuration;

public sealed record RunnerOptions(
    string DatasetPath,
    string OutputDirectory,
    string Mode,
    string Model)
{
    private const string DefaultDatasetPath =
        "docs/recherche/rag-agentique/evaluation-cases.json";

    public static RunnerOptions Parse(IReadOnlyList<string> arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < arguments.Count; index += 2)
        {
            if (index + 1 >= arguments.Count || !arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Arguments must use the format --name value.",
                    nameof(arguments));
            }

            values[arguments[index][2..]] = arguments[index + 1];
        }

        var mode = GetValue(values, "mode", "offline").ToLowerInvariant();
        if (mode is not ("offline" or "model"))
        {
            throw new ArgumentException(
                "The evaluation mode must be 'offline' or 'model'.",
                nameof(arguments));
        }

        return new RunnerOptions(
            GetValue(values, "dataset", DefaultDatasetPath),
            GetValue(values, "output", "artifacts/rag-evaluation"),
            mode,
            GetValue(values, "model", "gpt-5.6-luna"));
    }

    private static string GetValue(
        IReadOnlyDictionary<string, string> values,
        string key,
        string defaultValue) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
}
