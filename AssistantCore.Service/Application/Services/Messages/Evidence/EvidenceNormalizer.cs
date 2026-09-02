using System.Security.Cryptography;
using System.Text;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Evidence;

namespace AssistantCore.Service.Application.Services.Messages.Evidence;

public sealed class EvidenceNormalizer : IEvidenceNormalizer
{
    public static IReadOnlyCollection<RetrievedEvidence> Limit(
        IReadOnlyCollection<RetrievedEvidence> evidence,
        int maximumResults)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        return new EvidenceNormalizer().Normalize(
            evidence.Select(item => new EvidenceCandidate(
                item.SourceType,
                item.Title,
                item.Content,
                item.Reference,
                item.Url,
                item.OccurredAt,
                item.RelevanceScore)).ToArray(),
            new EvidenceNormalizationOptions(int.MaxValue, maximumResults));
    }

    public IReadOnlyCollection<RetrievedEvidence> Normalize(
        IReadOnlyCollection<EvidenceCandidate> candidates,
        EvidenceNormalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ValidateOptions(options);

        return candidates
            .Select(candidate => NormalizeCandidate(candidate, options.MaximumContentLength))
            .OfType<NormalizedCandidate>()
            .GroupBy(candidate => new EvidenceKey(
                candidate.SourceType,
                candidate.Reference))
            .Select(group => OrderByPreference(group).First())
            .OrderByDescending(candidate => candidate.RelevanceScore.HasValue)
            .ThenByDescending(candidate => candidate.RelevanceScore)
            .ThenByDescending(candidate => candidate.OriginalContentLength)
            .ThenBy(candidate => candidate.SourceType, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Reference, StringComparer.Ordinal)
            .Take(options.MaximumResults)
            .Select(CreateEvidence)
            .ToArray();
    }

    private static IOrderedEnumerable<NormalizedCandidate> OrderByPreference(
        IEnumerable<NormalizedCandidate> candidates) =>
        candidates
            .OrderByDescending(candidate => candidate.RelevanceScore.HasValue)
            .ThenByDescending(candidate => candidate.RelevanceScore)
            .ThenByDescending(candidate => candidate.OriginalContentLength);

    private static NormalizedCandidate? NormalizeCandidate(
        EvidenceCandidate? candidate,
        int maximumContentLength)
    {
        if (candidate is null
            || string.IsNullOrWhiteSpace(candidate.SourceType)
            || string.IsNullOrWhiteSpace(candidate.Title)
            || string.IsNullOrWhiteSpace(candidate.Content)
            || string.IsNullOrWhiteSpace(candidate.Reference))
        {
            return null;
        }

        var content = candidate.Content.Trim();
        double? relevanceScore = candidate.RelevanceScore is double score
            && double.IsFinite(score)
                ? score
                : null;

        return new NormalizedCandidate(
            candidate.SourceType.Trim(),
            candidate.Title.Trim(),
            content[..Math.Min(content.Length, maximumContentLength)],
            candidate.Reference.Trim(),
            string.IsNullOrWhiteSpace(candidate.Url) ? null : candidate.Url.Trim(),
            candidate.OccurredAt,
            relevanceScore,
            content.Length);
    }

    private static RetrievedEvidence CreateEvidence(NormalizedCandidate candidate) => new(
        CreateEvidenceId(candidate.SourceType, candidate.Reference),
        candidate.SourceType,
        candidate.Title,
        candidate.Content,
        candidate.Reference,
        candidate.Url,
        candidate.OccurredAt,
        candidate.RelevanceScore);

    private static string CreateEvidenceId(string sourceType, string reference)
    {
        var identifierBytes = Encoding.UTF8.GetBytes($"{sourceType}\u001f{reference}");
        var hash = SHA256.HashData(identifierBytes);
        return $"evidence-{Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant()}";
    }

    private static void ValidateOptions(EvidenceNormalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaximumContentLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The maximum content length must be greater than zero.");
        }

        if (options.MaximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The maximum number of results must be greater than zero.");
        }
    }

    private sealed record EvidenceKey(string SourceType, string Reference);

    private sealed record NormalizedCandidate(
        string SourceType,
        string Title,
        string Content,
        string Reference,
        string? Url,
        DateTimeOffset? OccurredAt,
        double? RelevanceScore,
        int OriginalContentLength);
}
