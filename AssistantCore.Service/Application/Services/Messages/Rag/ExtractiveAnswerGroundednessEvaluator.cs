using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AssistantCore.Service.Application.Models.Messages.Rag;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

// Lexical screening with numeric checks; this does not establish semantic entailment.
public sealed partial class ExtractiveAnswerGroundednessEvaluator : IAnswerGroundednessEvaluator
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "alors", "avec", "aux", "car", "cette", "ces", "dans", "des", "donc", "elle", "elles", "est", "etre",
        "les", "leur", "leurs", "mais", "par", "pas", "plus", "pour", "que", "qui", "sans", "ses", "sur", "une",
        "the", "and", "for", "that", "this", "with"
    };

    public Task<GroundednessResult> EvaluateAsync(string answer, IReadOnlyCollection<RagPassage> evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var claims = Sentences().Split(answer).Select(Normalize).Where(s => s.Length > 0).ToArray();
        var sourceText = Normalize(string.Join(' ', evidence.Select(p => p.Content)));
        var sourceTerms = Terms().Matches(RemoveDiacritics(sourceText))
            .Select(match => match.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceSentences = evidence.SelectMany(p => Sentences().Split(p.Content))
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var numericGrounding = GroundedAnswerNumbers.Evaluate(answer, GroundedAnswerNumbers.Read(sourceText));
        var numbersSupported = HasSufficientNumberSupport(numericGrounding);
        var supported = claims.Count(claim =>
            sourceSentences.Contains(claim)
            || HasEnoughEvidenceTerms(claim, sourceTerms));
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

        return result.SupportedNumberCount > 0
            && result.UnsupportedNumberCount <= result.SupportedNumberCount;
    }

    private static bool HasSufficientClaimSupport(int claimCount, int supported)
    {
        if (claimCount == 0)
            return false;

        if (supported == claimCount)
            return true;

        return supported > 0 && (double)supported / claimCount >= 0.6;
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
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:[,.]\p{N}+)?")]
    private static partial Regex Terms();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
