using System.Text.RegularExpressions;

namespace AssistantCore.Service.Application.Services.Conversations;

public static partial class ConversationTitleFactory
{
    private const int MaximumTitleLength = 200;
    private const string TruncationSuffix = "…";

    /// <summary>
    /// Reduit toute suite d'espaces a un seul caractere et retire les espaces de bordure.
    /// Deux titres qui ne different que par leurs espaces sont donc consideres identiques.
    /// </summary>
    public static string Normalize(string title) =>
        WhitespaceRegex().Replace(title, " ").Trim();

    public static string CreateFromFirstMessage(string message)
    {
        var singleLine = Normalize(message);

        if (singleLine.Length <= MaximumTitleLength)
        {
            return singleLine;
        }

        var truncatedLength = MaximumTitleLength - TruncationSuffix.Length;

        return singleLine[..truncatedLength] + TruncationSuffix;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
