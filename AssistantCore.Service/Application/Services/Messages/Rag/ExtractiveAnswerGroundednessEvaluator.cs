using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AssistantCore.Service.Application.Models.Messages.Rag;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

// Lexical screening with numeric checks; this does not establish semantic entailment.
public sealed partial class ExtractiveAnswerGroundednessEvaluator : IAnswerGroundednessEvaluator
{
    /// <summary>
    /// Au-dela de ce nombre de termes, une affirmation est traitee comme une phrase
    /// composee : le recouvrement lexical n'y distingue plus une fusion abusive de
    /// deux sources d'une synthese legitime.
    /// </summary>
    private const int MaximumSimpleClaimTerms = 8;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "alors", "avec", "aux", "car", "cette", "ces", "dans", "des", "donc", "elle", "elles", "est", "etre",
        "les", "leur", "leurs", "mais", "par", "pas", "plus", "pour", "que", "qui", "sans", "ses", "sur", "une",
        "the", "and", "for", "that", "this", "with"
    };

    public Task<GroundednessResult> EvaluateAsync(string answer, IReadOnlyCollection<RagPassage> evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var claims = Sentences().Split(ListMarkers().Replace(answer, string.Empty))
            .Select(Normalize)
            .Where(claim => claim.Length > 0)
            .ToArray();
        var sourceText = Normalize(string.Join(' ', evidence.Select(p => p.Content)));
        var sourceNumbers = GroundedAnswerNumbers.Read(sourceText);
        // Les termes sont regroupes par document et non mis en commun. Une phrase
        // qui emprunte ses termes a deux documents differents trouverait chacun
        // d'eux dans un ensemble commun, alors qu'aucun document ne soutient
        // l'affirmation complete : « Le conge parental necessite un justificatif »
        // passerait avec un document parlant du conge parental et un autre du
        // justificatif en conge maladie.
        var termsByDocument = evidence
            .GroupBy(passage => passage.SourceIdentity, StringComparer.Ordinal)
            .Select(group => Terms()
                .Matches(RemoveDiacritics(Normalize(string.Join(' ', group.Select(passage => passage.Content)))))
                .Select(match => match.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var sourceSentences = evidence.SelectMany(p => Sentences().Split(p.Content))
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var numericGrounding = GroundedAnswerNumbers.Evaluate(answer, sourceNumbers);
        var numbersSupported = HasSufficientNumberSupport(numericGrounding);
        var supported = claims.Count(claim =>
            sourceSentences.Contains(claim)
            // Une affirmation doit tenir dans un seul document. Une reponse peut
            // toujours synthetiser plusieurs sources, chaque phrase etant alors
            // rattachee a celle qui la soutient.
            || termsByDocument.Any(documentTerms =>
                HasEnoughEvidenceTerms(claim, documentTerms)
                && !BorrowsTermsFromAnotherDocument(claim, documentTerms, termsByDocument))
            || HasFullyGroundedDerivedNumbers(claim, sourceNumbers));
        var lexicalConfidence = claims.Length == 0 ? 0 : (double)supported / claims.Length;
        var passed = numbersSupported && HasSufficientClaimSupport(claims.Length, supported);
        var confidence = passed ? Math.Max(lexicalConfidence, 0.75) : numbersSupported ? lexicalConfidence : 0;
        return Task.FromResult(new GroundednessResult(passed,
            confidence, !numbersSupported ? numericGrounding.Reason : supported == claims.Length ? "All claims are covered by the evidence." : "One or more claims cannot be verified from evidence terms."));
    }

    private static bool HasSufficientNumberSupport(NumericGroundingResult result)
    {
        if (result.HasBlockingFailure)
            return false;

        if (result.UnsupportedNumberCount == 0)
            return true;

        return result.SupportedNumberCount > result.UnsupportedNumberCount;
    }

    private static bool HasFullyGroundedDerivedNumbers(
        string claim,
        IReadOnlySet<decimal> sourceNumbers)
    {
        var result = GroundedAnswerNumbers.Evaluate(claim, sourceNumbers);
        return !result.HasBlockingFailure
            && result.SupportedNumberCount > 0
            && result.UnsupportedNumberCount == 0
            && GroundedAnswerNumbers.HasDerivedNumber(claim, sourceNumbers);
    }

    private static bool HasSufficientClaimSupport(int claimCount, int supported)
    {
        if (claimCount == 0)
            return false;

        if (supported == claimCount)
            return true;

        return supported > 0 && (double)supported / claimCount >= 0.6;
    }

    /// <summary>
    /// Detecte qu'une affirmation emprunte un terme a un autre document que celui
    /// examine. Un terme absent de toutes les preuves est du vocabulaire du modele,
    /// sans consequence; un terme present ailleurs mais pas ici signale au contraire
    /// que l'affirmation melange deux sources.
    ///
    /// « Le conge parental necessite un justificatif » recouvre assez le document
    /// traitant du conge maladie, mais « parental » appartient a l'autre document :
    /// aucun des deux ne soutient l'affirmation complete.
    /// </summary>
    private static bool BorrowsTermsFromAnotherDocument(
        string claim,
        IReadOnlySet<string> documentTerms,
        IReadOnlyCollection<HashSet<string>> termsByDocument)
    {
        if (termsByDocument.Count < 2) return false;

        var claimTerms = Terms().Matches(RemoveDiacritics(claim))
            .Select(match => match.Value)
            .Where(term => term.Length >= 3 && !StopWords.Contains(term) && !term.Any(char.IsDigit))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // La detection se limite a une proposition courte et simple. Une phrase longue
        // ou coordonnee enchaine des faits distincts, chacun pouvant legitimement venir
        // d'un document different : « la marge de MetalPro est de 18 %, et la creance de
        // Nordik atteint 12000 $ » reste une synthese correcte. Sur une telle phrase, un
        // simple recouvrement de termes ne permet pas de decider, et le signalement
        // rejetterait des reponses valides.
        if (claimTerms.Length > MaximumSimpleClaimTerms || Clauses().IsMatch(claim)) return false;

        return claimTerms.Any(term => !documentTerms.Contains(term)
            && termsByDocument.Any(other => other != documentTerms && other.Contains(term)));
    }

    private static bool HasEnoughEvidenceTerms(string claim, IReadOnlySet<string> sourceTerms)
    {
        var claimTerms = Terms().Matches(RemoveDiacritics(claim))
            .Select(match => match.Value)
            .Where(term => term.Length >= 3 && !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (claimTerms.Length == 0) return false;

        var textTerms = claimTerms
            .Where(term => !term.Any(char.IsDigit))
            .ToArray();
        if (textTerms.Length == 0) return true;

        var matchedTextTerms = textTerms.Count(sourceTerms.Contains);
        return matchedTextTerms >= Math.Min(3, textTerms.Length);
    }

    private static string Normalize(string value) => Whitespace().Replace(value.Trim().Trim('*', '-', ' ', '"', '«', '»'), " ");

    private static string RemoveDiacritics(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
        .Normalize(NormalizationForm.FormC);

    [GeneratedRegex(@"[.!?](?:\s+|$)|[\r\n]+")]
    private static partial Regex Sentences();
    [GeneratedRegex(@"^\s*\d+[.)]\s+", RegexOptions.Multiline)]
    private static partial Regex ListMarkers();
    [GeneratedRegex(@"[;,]\s*(?:et|mais|ou|ainsi que|tandis que|alors que|and|but|while)|[;:]", RegexOptions.IgnoreCase)]
    private static partial Regex Clauses();
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:[,.]\p{N}+)?")]
    private static partial Regex Terms();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
