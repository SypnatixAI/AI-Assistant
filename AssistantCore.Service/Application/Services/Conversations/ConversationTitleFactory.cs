using System.Text.RegularExpressions;

namespace AssistantCore.Service.Application.Services.Conversations;

public static partial class ConversationTitleFactory
{
    private const int MaximumTitleLength = 200;
    private const string TruncationSuffix = "…";

    public static string CreateFromFirstMessage(string message)
    {
        var singleLine = WhitespaceRegex().Replace(message, " ").Trim();

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
