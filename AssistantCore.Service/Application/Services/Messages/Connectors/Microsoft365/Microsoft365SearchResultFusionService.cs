using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public sealed class Microsoft365SearchResultFusionService : IMicrosoft365SearchResultFusionService
{
    private const int ReciprocalRankFusionConstant = 60;

    public IReadOnlyCollection<Microsoft365SearchRecord> Fuse(
        IReadOnlyCollection<IReadOnlyCollection<Microsoft365SearchRecord>> resultSets,
        int maximumResults)
    {
        ArgumentNullException.ThrowIfNull(resultSets);
        if (maximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }

        var recordsByChunk = new Dictionary<string, FusedRecord>(StringComparer.Ordinal);

        foreach (var resultSet in resultSets)
        {
            AddResultSet(recordsByChunk, resultSet);
        }

        return recordsByChunk.Values
            .OrderByDescending(record => record.FusionScore)
            .ThenBy(record => record.BestRank)
            .ThenBy(record => record.Record.Reference, StringComparer.Ordinal)
            .Take(maximumResults)
            .Select(record => record.Record with { RelevanceScore = record.FusionScore })
            .ToArray();
    }

    private static void AddResultSet(
        Dictionary<string, FusedRecord> recordsByChunk,
        IReadOnlyCollection<Microsoft365SearchRecord> resultSet)
    {
        var rank = 0;
        foreach (var record in resultSet)
        {
            rank++;
            AddRecord(recordsByChunk, record, rank);
        }
    }

    private static void AddRecord(
        Dictionary<string, FusedRecord> recordsByChunk,
        Microsoft365SearchRecord record,
        int rank)
    {
        if (string.IsNullOrWhiteSpace(record.Reference))
        {
            return;
        }

        var reciprocalRankScore = CalculateReciprocalRankScore(rank);
        if (!recordsByChunk.TryGetValue(record.Reference, out var fusedRecord))
        {
            recordsByChunk[record.Reference] = new FusedRecord(
                record,
                reciprocalRankScore,
                rank);
            return;
        }

        recordsByChunk[record.Reference] = fusedRecord.Add(
            record,
            reciprocalRankScore,
            rank);
    }

    private static double CalculateReciprocalRankScore(int rank) =>
        1d / (ReciprocalRankFusionConstant + rank);

    private sealed record FusedRecord(
        Microsoft365SearchRecord Record,
        double FusionScore,
        int BestRank)
    {
        public FusedRecord Add(
            Microsoft365SearchRecord candidate,
            double reciprocalRankScore,
            int candidateRank)
        {
            var bestRecord = candidateRank < BestRank
                || (candidateRank == BestRank
                    && (candidate.RelevanceScore ?? 0d) > (Record.RelevanceScore ?? 0d))
                    ? candidate
                    : Record;

            return new FusedRecord(
                bestRecord,
                FusionScore + reciprocalRankScore,
                Math.Min(BestRank, candidateRank));
        }
    }
}
