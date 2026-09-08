using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Rag;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

/// <summary>
/// Reduit la redondance des preuves apres classement. Cinq passages consecutifs
/// du meme document sont chacun pertinents, mais laissent peu de place a une
/// seconde source pourtant necessaire a une question multi-document.
///
/// La strategie est volontairement une heuristique et non un MMR complet : elle
/// n'a besoin d'aucun vecteur d'embedding au moment de la reponse, donc aucun
/// appel supplementaire ni cout associe.
///
/// Elle ne contourne jamais le classement. Un passage n'est promu que si son
/// score reste proche de celui qu'il depasse, et l'ordre relatif des passages
/// d'une meme source est toujours conserve.
/// </summary>
public sealed class RagPassageDiversifier(IOptions<RagOptions> options) : IRagPassageDiversifier
{
    public IReadOnlyList<RagPassage> Diversify(IReadOnlyList<RagPassage> ranked)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        var settings = options.Value.Diversification;
        if (!settings.Enabled || ranked.Count <= 1) return ranked;

        var remaining = RemoveNearDuplicates(ranked, settings.NearDuplicateOverlapThreshold);
        var selected = new List<RagPassage>(remaining.Count);
        var consecutive = 0;
        string? previousSource = null;

        while (remaining.Count > 0)
        {
            var index = 0;
            if (previousSource is not null
                && consecutive >= settings.MaximumConsecutiveFromSameSource
                && string.Equals(remaining[0].SourceIdentity, previousSource, StringComparison.Ordinal))
            {
                // Le meilleur candidat vient encore de la source saturee : chercher
                // le premier passage d'une autre source, et ne le promouvoir que si
                // son score reste dans l'ecart tolere.
                var alternative = FindPromotableAlternative(remaining, previousSource, settings.MaximumPromotionScoreGap);
                if (alternative > 0) index = alternative;
            }

            var chosen = remaining[index];
            remaining.RemoveAt(index);
            selected.Add(chosen);
            consecutive = string.Equals(chosen.SourceIdentity, previousSource, StringComparison.Ordinal)
                ? consecutive + 1
                : 1;
            previousSource = chosen.SourceIdentity;
        }

        RagTelemetry.Record("rag.diversification.removed_count", ranked.Count - selected.Count);
        RagTelemetry.Record(
            "rag.diversification.distinct_sources",
            selected.Select(passage => passage.SourceIdentity).Distinct(StringComparer.Ordinal).Count());
        return selected;
    }

    private static int FindPromotableAlternative(
        List<RagPassage> remaining,
        string saturatedSource,
        double maximumGap)
    {
        var leadingScore = remaining[0].SemanticScore;
        for (var index = 1; index < remaining.Count; index++)
        {
            if (string.Equals(remaining[index].SourceIdentity, saturatedSource, StringComparison.Ordinal)) continue;
            // Sans score des deux cotes, aucune comparaison n'est possible : le
            // classement d'origine fait alors autorite.
            if (leadingScore is null || remaining[index].SemanticScore is null) return 0;
            return leadingScore.Value - remaining[index].SemanticScore!.Value <= maximumGap ? index : 0;
        }

        return 0;
    }

    /// <summary>
    /// Ecarte un passage dont le contenu recouvre presque entierement celui d'un
    /// passage deja retenu de la meme source. La comparaison reste limitee a une
    /// meme source : deux documents differents qui se citent l'un l'autre restent
    /// deux preuves distinctes.
    /// </summary>
    private static List<RagPassage> RemoveNearDuplicates(IReadOnlyList<RagPassage> ranked, double threshold)
    {
        var kept = new List<RagPassage>(ranked.Count);
        var tokensBySource = new Dictionary<string, List<HashSet<string>>>(StringComparer.Ordinal);

        foreach (var passage in ranked)
        {
            var tokens = Tokenize(passage.Content);
            if (!tokensBySource.TryGetValue(passage.SourceIdentity, out var previous))
            {
                previous = [];
                tokensBySource[passage.SourceIdentity] = previous;
            }

            if (tokens.Count > 0 && previous.Any(earlier => Overlap(tokens, earlier) >= threshold)) continue;

            previous.Add(tokens);
            kept.Add(passage);
        }

        return kept;
    }

    private static double Overlap(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0) return 0;
        var shared = left.Count(token => right.Contains(token));
        // Rapporte au plus petit des deux : un extrait entierement contenu dans un
        // passage plus long n'apporte rien de neuf, meme si le long en dit plus.
        return (double)shared / Math.Min(left.Count, right.Count);
    }

    private static HashSet<string> Tokenize(string content) =>
        content
            .Split(
                [' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '"', '\''],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);
}
