using System.Globalization;
using System.Text.RegularExpressions;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

internal static partial class GroundedAnswerNumbers
{
    private const string NumberPattern = @"(?<![\p{L}\p{N}])[+-]?(?:\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d{1,3}(?:\.\d{3})+(?:,\d+)?|\d+(?:[ \u00a0\u202f]\d{3})*(?:[.,]\d+)?)(?![\p{L}\p{N}]|[.,]\d)";

    public static IReadOnlySet<decimal> Read(string text) => Numbers().Matches(text)
        .SelectMany(match => ParseCandidates(match.Value))
        .ToHashSet();

    public static string? GetFailureReason(string answer, IReadOnlySet<decimal> sourceNumbers)
    {
        var result = Evaluate(answer, sourceNumbers);
        return result.HasBlockingFailure ? result.Reason : null;
    }

    public static bool HasDerivedNumber(
        string answer,
        IReadOnlySet<decimal> sourceNumbers)
    {
        var hasNonSourceEquationResult = ComplementPercentageEquations().Matches(answer)
            .Cast<Match>()
            .Concat(Equations().Matches(answer).Cast<Match>())
            .SelectMany(match => ParseCandidates(match.Groups["result"].Value))
            .Any(number => !sourceNumbers.Contains(number));
        if (hasNonSourceEquationResult)
            return true;

        return Numbers().Matches(answer)
            .Where(match => !IsStructuralNumber(answer, match))
            .SelectMany(match => ParseCandidates(match.Value))
            .Any(number =>
                !sourceNumbers.Contains(number)
                && IsSupportedByEvidenceOrCalculation(number, sourceNumbers));
    }

    public static NumericGroundingResult Evaluate(string answer, IReadOnlySet<decimal> sourceNumbers)
    {
        var justifiedNumbers = sourceNumbers.ToHashSet();
        AddDerivableAnswerNumbers(answer, justifiedNumbers);
        var handledEquationRanges = new List<(int Start, int End)>();
        foreach (Match equation in ComplementPercentageEquations().Matches(answer))
        {
            var amount = ResolveEquationOperand(
                equation.Groups["amount"].Value,
                false,
                justifiedNumbers);
            var baseValues = ResolveComplementBaseOperand(
                equation.Groups["base"].Value,
                equation.Groups["basePercent"].Success,
                justifiedNumbers);
            var rate = ResolveEquationOperand(
                equation.Groups["rate"].Value,
                true,
                justifiedNumbers);
            var results = ParseCandidates(equation.Groups["result"].Value).ToArray();
            if (amount.Length == 0 || baseValues.Length == 0 || rate.Length == 0 || results.Length == 0)
                return NumericGroundingResult.Blocked(CreateMissingComplementOperandReason(equation, amount, baseValues, rate, results));

            var verifiedResult = TryVerifyComplementPercentageCalculation(
                amount,
                baseValues,
                equation.Groups["innerOperator"].Value,
                rate,
                results,
                equation.Groups["resultPercent"].Success);
            if (verifiedResult is null)
                return NumericGroundingResult.Blocked($"Calculation result is incorrect or operation is undefined (position {equation.Index}).");

            foreach (var baseValue in baseValues)
                justifiedNumbers.Add(baseValue);
            justifiedNumbers.Add(verifiedResult.Value);
            if (equation.Groups["resultPercent"].Success)
                justifiedNumbers.Add(verifiedResult.Value / 100);
            handledEquationRanges.Add((equation.Index, equation.Index + equation.Length));
        }

        foreach (Match equation in Equations().Matches(answer))
        {
            if (IsInsideHandledRange(equation, handledEquationRanges))
                continue;

            var left = ResolveEquationOperand(
                equation.Groups["left"].Value,
                equation.Groups["leftPercent"].Success,
                justifiedNumbers);
            var right = ResolveEquationOperand(
                equation.Groups["right"].Value,
                equation.Groups["rightPercent"].Success,
                justifiedNumbers);
            var results = ParseCandidates(equation.Groups["result"].Value).ToArray();
            if (left.Length == 0 || right.Length == 0 || results.Length == 0)
                return NumericGroundingResult.Blocked(CreateMissingOperandReason(equation, left, right, results));

            var verifiedResult = TryVerifyCalculation(
                equation.Groups["operator"].Value, left, right, results,
                equation.Groups["resultPercent"].Success);
            if (verifiedResult is null)
                return NumericGroundingResult.Blocked($"Calculation result is incorrect or operation is undefined (position {equation.Index}).");

            justifiedNumbers.Add(verifiedResult.Value);
            if (equation.Groups["resultPercent"].Success)
                justifiedNumbers.Add(verifiedResult.Value / 100);
        }

        var supportedNumbers = new HashSet<decimal>();
        var unsupportedNumbers = new HashSet<decimal>();
        string? firstUnsupportedReason = null;
        foreach (Match match in Numbers().Matches(answer))
        {
            if (IsStructuralNumber(answer, match))
                continue;

            var candidates = ParseCandidates(match.Value).ToArray();
            if (candidates.Length == 0)
                return NumericGroundingResult.Blocked($"Unparseable number (position {match.Index}).");
            var supportedCandidates = candidates
                .Where(candidate => IsSupportedByEvidenceOrCalculation(candidate, justifiedNumbers))
                .ToArray();
            if (supportedCandidates.Length == 0)
            {
                unsupportedNumbers.Add(candidates[0]);
                firstUnsupportedReason ??= $"Number {candidates[0].ToString(CultureInfo.InvariantCulture)} at position {match.Index} is absent from cited evidence and verified calculations.";
            }
            else
            {
                supportedNumbers.Add(supportedCandidates[0]);
            }
        }

        return new NumericGroundingResult(
            false,
            firstUnsupportedReason,
            supportedNumbers.Count,
            unsupportedNumbers.Count);
    }

    private static bool IsInsideHandledRange(Match equation, IReadOnlyCollection<(int Start, int End)> ranges) =>
        ranges.Any(range => equation.Index >= range.Start && equation.Index + equation.Length <= range.End);

    private static void AddDerivableAnswerNumbers(string answer, HashSet<decimal> justifiedNumbers)
    {
        foreach (Match match in Numbers().Matches(answer))
        {
            if (IsStructuralNumber(answer, match))
                continue;

            foreach (var candidate in ParseCandidates(match.Value))
            {
                if (IsSupportedByEvidenceOrCalculation(candidate, justifiedNumbers))
                    justifiedNumbers.Add(candidate);
            }
        }
    }

    private static decimal[] ResolveEquationOperand(
        string value,
        bool isPercentage,
        IReadOnlySet<decimal> sourceNumbers)
    {
        return ParseCandidates(value).SelectMany(number =>
            ResolveSupportedEquationOperand(number, isPercentage, sourceNumbers)).ToArray();
    }

    private static IEnumerable<decimal> ResolveSupportedEquationOperand(
        decimal number,
        bool isPercentage,
        IReadOnlySet<decimal> sourceNumbers)
    {
        if (!isPercentage)
        {
            if (IsSupportedByEvidenceOrCalculation(number, sourceNumbers))
                yield return number;
            if (number is > 0 and <= 1 && IsSupportedByEvidenceOrCalculation(number * 100, sourceNumbers))
                yield return number;
            yield break;
        }

        var ratio = number / 100;
        if (IsSupportedByEvidenceOrCalculation(number, sourceNumbers)
            || IsSupportedByEvidenceOrCalculation(ratio, sourceNumbers))
        {
            yield return ratio;
        }
    }

    private static string CreateMissingOperandReason(
        Match equation,
        IReadOnlyCollection<decimal> left,
        IReadOnlyCollection<decimal> right,
        IReadOnlyCollection<decimal> results)
    {
        var missing = new List<string>();
        if (left.Count == 0) missing.Add("left");
        if (right.Count == 0) missing.Add("right");
        if (results.Count == 0) missing.Add("result");

        return $"Calculation operands are missing or unparseable ({string.Join(", ", missing)}; position {equation.Index}).";
    }

    private static string CreateMissingComplementOperandReason(
        Match equation,
        IReadOnlyCollection<decimal> amount,
        IReadOnlyCollection<decimal> baseValues,
        IReadOnlyCollection<decimal> rate,
        IReadOnlyCollection<decimal> results)
    {
        var missing = new List<string>();
        if (amount.Count == 0) missing.Add("amount");
        if (baseValues.Count == 0) missing.Add("base");
        if (rate.Count == 0) missing.Add("rate");
        if (results.Count == 0) missing.Add("result");

        return $"Calculation operands are missing or unparseable ({string.Join(", ", missing)}; position {equation.Index}).";
    }

    private static decimal[] ResolveComplementBaseOperand(
        string value,
        bool isPercentage,
        IReadOnlySet<decimal> sourceNumbers)
    {
        var parsedValues = ParseCandidates(value).ToArray();
        var resolvedValues = parsedValues.SelectMany(number =>
            ResolveSupportedEquationOperand(number, isPercentage, sourceNumbers)).ToList();

        foreach (var number in parsedValues)
        {
            if (!isPercentage && number == 1 && !resolvedValues.Contains(1))
                resolvedValues.Add(1);
            if (isPercentage && number == 100 && !resolvedValues.Contains(1))
                resolvedValues.Add(1);
        }

        return resolvedValues.ToArray();
    }

    private static bool IsSupportedByEvidenceOrCalculation(
        decimal number,
        IReadOnlySet<decimal> sourceNumbers)
    {
        if (sourceNumbers.Contains(number))
            return true;

        var values = sourceNumbers.ToArray();
        try
        {
            for (var i = 0; i < values.Length; i++)
            {
                for (var j = 0; j < values.Length; j++)
                {
                    if (i == j)
                        continue;

                    var left = values[i];
                    var right = values[j];
                    if (MatchesCalculatedResult(number, left + right)
                        || MatchesCalculatedResult(number, left - right)
                        || MatchesCalculatedResult(number, left * right)
                        || right != 0 && MatchesCalculatedResult(number, left / right)
                        || right != 0 && IsPercentageRatio(number, left, right)
                        || IsPercentageProduct(number, left, right))
                    {
                        return true;
                    }
                }
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        return false;
    }

    private static bool IsPercentageProduct(decimal number, decimal amount, decimal percentage) =>
        percentage is > 0 and <= 100
        && MatchesCalculatedResult(number, amount * percentage / 100);

    private static bool IsPercentageRatio(decimal number, decimal left, decimal right) =>
        number is > 0 and <= 100
        && MatchesCalculatedResult(number, left / right * 100);

    private static decimal? TryVerifyCalculation(
        string operatorValue,
        IReadOnlyCollection<decimal> left,
        IReadOnlyCollection<decimal> right,
        IReadOnlyCollection<decimal> results,
        bool isResultPercentage)
    {
        try
        {
            foreach (var a in left)
            {
                foreach (var b in right)
                {
                    decimal? calculated = operatorValue switch
                    {
                        "+" => a + b,
                        "-" or "−" => a - b,
                        "*" or "×" => a * b,
                        "/" or "÷" when b != 0 => a / b,
                        _ => null
                    };
                    if (calculated is not decimal value)
                        continue;

                    var displayedValue = isResultPercentage ? value * 100 : value;
                    foreach (var result in results)
                    {
                        if (MatchesCalculatedResult(result, displayedValue))
                            return result;
                    }
                }
            }
        }
        catch (OverflowException)
        {
            return null;
        }

        return null;
    }

    private static decimal? TryVerifyComplementPercentageCalculation(
        IReadOnlyCollection<decimal> amounts,
        IReadOnlyCollection<decimal> baseValues,
        string operatorValue,
        IReadOnlyCollection<decimal> rates,
        IReadOnlyCollection<decimal> results,
        bool isResultPercentage)
    {
        try
        {
            foreach (var amount in amounts)
            {
                foreach (var baseValue in baseValues)
                {
                    foreach (var rate in rates)
                    {
                        decimal? factor = operatorValue switch
                        {
                            "+" => baseValue + rate,
                            "-" or "−" => baseValue - rate,
                            _ => null
                        };
                        if (factor is not decimal resolvedFactor)
                            continue;

                        var displayedValue = amount * resolvedFactor;
                        if (isResultPercentage)
                            displayedValue *= 100;

                        foreach (var result in results)
                        {
                            if (MatchesCalculatedResult(result, displayedValue))
                                return result;
                        }
                    }
                }
            }
        }
        catch (OverflowException)
        {
            return null;
        }

        return null;
    }

    private static bool MatchesCalculatedResult(decimal stated, decimal calculated)
    {
        if (stated == calculated)
            return true;

        // Only calculated decimal results may be rounded, at their displayed precision.
        // Integer claims and values copied from evidence still require exact equality.
        var decimalPlaces = (decimal.GetBits(stated)[3] >> 16) & 0xff;
        return decimalPlaces > 0
            && decimal.Round(calculated, decimalPlaces, MidpointRounding.AwayFromZero) == stated;
    }

    private static IReadOnlyCollection<decimal> ParseCandidates(string value)
    {
        var normalizedSpaces = value.Replace(" ", "").Replace("\u00a0", "").Replace("\u202f", "");
        var candidates = new List<decimal>();
        if (normalizedSpaces.Count(character => character is ',' or '.') > 1)
        {
            var hasBothSeparators = normalizedSpaces.Contains(',') && normalizedSpaces.Contains('.');
            var decimalSeparator = normalizedSpaces.LastIndexOf(',') > normalizedSpaces.LastIndexOf('.') ? ',' : '.';
            var groupingSeparator = decimalSeparator == ',' ? '.' : ',';
            var normalized = hasBothSeparators
                ? normalizedSpaces.Replace(groupingSeparator.ToString(), "").Replace(decimalSeparator, '.')
                : normalizedSpaces.Replace(",", "").Replace(".", "");
            AddCandidate(normalized, candidates);
            return candidates;
        }

        AddCandidate(normalizedSpaces.Replace(',', '.'), candidates);

        if (HasSingleThousandsSeparator(normalizedSpaces))
            AddCandidate(normalizedSpaces.Replace(",", "").Replace(".", ""), candidates);

        return candidates;
    }

    private static bool HasSingleThousandsSeparator(string value)
    {
        var separatorIndex = value.IndexOfAny([',', '.']);
        return separatorIndex > 0
            && separatorIndex == value.LastIndexOfAny([',', '.'])
            && value.Length - separatorIndex - 1 == 3;
    }

    private static void AddCandidate(string value, List<decimal> candidates)
    {
        if (decimal.TryParse(
                value,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var number)
            && !candidates.Contains(number))
        {
            candidates.Add(number);
        }
    }

    private static bool IsStructuralNumber(string text, Match match)
    {
        if (IsCitationNumber(text, match))
            return true;

        var index = match.Index - 1;
        while (index >= 0 && text[index] is ' ' or '\t')
            index--;

        var atLineStart = index < 0 || text[index] is '\r' or '\n';
        if (!atLineStart)
            return false;

        var nextIndex = match.Index + match.Length;
        if (nextIndex >= text.Length || text[nextIndex] is not ('.' or ')'))
            return false;

        var afterMarker = nextIndex + 1;
        return afterMarker >= text.Length || char.IsWhiteSpace(text[afterMarker]);
    }

    private static bool IsCitationNumber(string text, Match match)
    {
        var previousIndex = match.Index - 1;
        var nextIndex = match.Index + match.Length;
        if (previousIndex < 0 || nextIndex >= text.Length)
            return false;

        return (text[previousIndex], text[nextIndex]) switch
        {
            ('[', ']') => true,
            ('(', ')') => true,
            ('【', '†') => true,
            _ => false
        };
    }

    [GeneratedRegex(NumberPattern)]
    private static partial Regex Numbers();

    [GeneratedRegex(@"(?<left>" + NumberPattern + @")\s*(?<leftPercent>%)?\s*(?<operator>[+−*/×÷-])\s*(?<right>" + NumberPattern + @")\s*(?<rightPercent>%)?\s*=\s*(?<result>" + NumberPattern + @")\s*(?<resultPercent>%)?")]
    private static partial Regex Equations();

    [GeneratedRegex(@"(?<amount>" + NumberPattern + @")\s*(?:\*|×)\s*\(\s*(?<base>" + NumberPattern + @")\s*(?<basePercent>%)?\s*(?<innerOperator>[+−-])\s*(?<rate>" + NumberPattern + @")\s*(?<ratePercent>%)\s*\)\s*=\s*(?<result>" + NumberPattern + @")\s*(?<resultPercent>%)?")]
    private static partial Regex ComplementPercentageEquations();
}

internal sealed record NumericGroundingResult(
    bool HasBlockingFailure,
    string? Reason,
    int SupportedNumberCount,
    int UnsupportedNumberCount)
{
    public static NumericGroundingResult Blocked(string reason) => new(true, reason, 0, 0);
}
