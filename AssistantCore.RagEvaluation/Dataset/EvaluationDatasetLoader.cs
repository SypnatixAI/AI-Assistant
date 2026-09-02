using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.RagEvaluation.Models;

namespace AssistantCore.RagEvaluation.Dataset;

public sealed class EvaluationDatasetLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<EvaluationDataset> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The dataset path is required.", nameof(path));
        }

        await using var stream = File.OpenRead(path);
        var dataset = await JsonSerializer.DeserializeAsync<EvaluationDataset>(
            stream,
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidDataException("The evaluation dataset is empty.");

        Validate(dataset);
        return dataset;
    }

    private static void Validate(EvaluationDataset dataset)
    {
        if (dataset.Version != 2)
        {
            throw new InvalidDataException("The evaluation dataset version must be 2.");
        }

        if (string.IsNullOrWhiteSpace(dataset.Scope) || dataset.Cases.Count == 0)
        {
            throw new InvalidDataException("The evaluation dataset must define a scope and cases.");
        }

        var duplicateCaseId = dataset.Cases
            .GroupBy(evaluationCase => evaluationCase.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateCaseId is not null)
        {
            throw new InvalidDataException($"The case identifier '{duplicateCaseId}' is duplicated.");
        }

        foreach (var evaluationCase in dataset.Cases)
        {
            ValidateCase(evaluationCase);
        }
    }

    private static void ValidateCase(RagEvaluationCase evaluationCase)
    {
        if (string.IsNullOrWhiteSpace(evaluationCase.Id)
            || string.IsNullOrWhiteSpace(evaluationCase.Language)
            || evaluationCase.Conversation.Count == 0
            || evaluationCase.Conversation.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException(
                "Every evaluation case requires an identifier, language and conversation.");
        }

        if (evaluationCase.Modes.Count == 0
            || evaluationCase.Modes.Any(mode => mode is not ("offline" or "model")))
        {
            throw new InvalidDataException(
                $"Case '{evaluationCase.Id}' must define valid execution modes.");
        }

        var duplicateReference = evaluationCase.Documents
            .GroupBy(document => document.Reference, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateReference is not null)
        {
            throw new InvalidDataException(
                $"Case '{evaluationCase.Id}' contains duplicate document reference '{duplicateReference}'.");
        }

        var knownReferences = evaluationCase.Documents
            .Select(document => document.Reference)
            .ToHashSet(StringComparer.Ordinal);
        var retrievedReferences = evaluationCase.Fixture.RetrievalRounds.SelectMany(round => round);
        var unknownFixtureReference = retrievedReferences
            .Concat(evaluationCase.Expected.ExpectedSourceReferences)
            .Concat(evaluationCase.Expected.ForbiddenSourceReferences)
            .FirstOrDefault(reference => !knownReferences.Contains(reference));
        if (unknownFixtureReference is not null)
        {
            throw new InvalidDataException(
                $"Case '{evaluationCase.Id}' references unknown document '{unknownFixtureReference}'.");
        }

        if (evaluationCase.Fixture.SearchQueries.Count
            < evaluationCase.Fixture.RetrievalRounds.Count)
        {
            throw new InvalidDataException(
                $"Case '{evaluationCase.Id}' requires one search query per retrieval round.");
        }
    }
}
