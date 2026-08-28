using System.Text.RegularExpressions;

namespace AssistantCore.Service.Application.Services.Conversations;

public static partial class ConversationPreviewFactory
{
    private const string TruncationSuffix = "…";

    public static string? Create(string? lastMessageContent, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(lastMessageContent))
        {
            return null;
        }

        var singleLine = WhitespaceRegex().Replace(lastMessageContent, " ").Trim();

        if (singleLine.Length <= maximumLength)
        {
            return singleLine;
        }

        var truncatedLength = maximumLength - TruncationSuffix.Length;

        return singleLine[..truncatedLength] + TruncationSuffix;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
