using System.Text;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

/// <summary>
/// Extracts the value of the <c>answer</c> JSON property while a structured model
/// response is received incrementally. It deliberately emits only decoded answer text.
/// </summary>
internal sealed class JsonAnswerDeltaExtractor
{
    private const string AnswerProperty = "\"answer\"";
    private readonly StringBuilder _propertyCandidate = new();
    private readonly StringBuilder _unicodeEscape = new();
    private ExtractorState _state = ExtractorState.LookingForProperty;

    public IReadOnlyCollection<string> Append(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var deltas = new List<string>();
        foreach (var character in text)
        {
            ProcessCharacter(character, deltas);
        }

        return deltas;
    }

    private void ProcessCharacter(char character, ICollection<string> deltas)
    {
        switch (_state)
        {
            case ExtractorState.LookingForProperty:
                _propertyCandidate.Append(character);
                if (_propertyCandidate.Length > AnswerProperty.Length)
                {
                    _propertyCandidate.Remove(0, 1);
                }

                if (_propertyCandidate.ToString() == AnswerProperty)
                {
                    _propertyCandidate.Clear();
                    _state = ExtractorState.LookingForColon;
                }

                return;
            case ExtractorState.LookingForColon:
                if (character == ':')
                {
                    _state = ExtractorState.LookingForValue;
                }

                return;
            case ExtractorState.LookingForValue:
                if (char.IsWhiteSpace(character))
                {
                    return;
                }

                _state = character == '"'
                    ? ExtractorState.ReadingValue
                    : ExtractorState.Completed;
                return;
            case ExtractorState.ReadingValue:
                if (character == '\\')
                {
                    _state = ExtractorState.ReadingEscape;
                }
                else if (character == '"')
                {
                    _state = ExtractorState.Completed;
                }
                else
                {
                    deltas.Add(character.ToString());
                }

                return;
            case ExtractorState.ReadingEscape:
                ProcessEscape(character, deltas);
                return;
            case ExtractorState.ReadingUnicodeEscape:
                _unicodeEscape.Append(character);
                if (_unicodeEscape.Length == 4)
                {
                    if (ushort.TryParse(
                            _unicodeEscape.ToString(),
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var value))
                    {
                        deltas.Add(((char)value).ToString());
                    }

                    _unicodeEscape.Clear();
                    _state = ExtractorState.ReadingValue;
                }

                return;
            case ExtractorState.Completed:
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ProcessEscape(char character, ICollection<string> deltas)
    {
        var decoded = character switch
        {
            '"' => "\"",
            '\\' => "\\",
            '/' => "/",
            'b' => "\b",
            'f' => "\f",
            'n' => "\n",
            'r' => "\r",
            't' => "\t",
            'u' => null,
            _ => string.Empty
        };

        if (decoded is null)
        {
            _unicodeEscape.Clear();
            _state = ExtractorState.ReadingUnicodeEscape;
            return;
        }

        if (decoded.Length > 0)
        {
            deltas.Add(decoded);
        }

        _state = ExtractorState.ReadingValue;
    }

    private enum ExtractorState
    {
        LookingForProperty,
        LookingForColon,
        LookingForValue,
        ReadingValue,
        ReadingEscape,
        ReadingUnicodeEscape,
        Completed
    }
}
