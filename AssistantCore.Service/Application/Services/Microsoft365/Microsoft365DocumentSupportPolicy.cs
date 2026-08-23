namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365DocumentSupportPolicy : IMicrosoft365DocumentSupportPolicy
{
    private static readonly HashSet<string> UnsupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aac", ".avi", ".flac", ".m4a", ".m4v", ".mkv", ".mov", ".mp3", ".mp4",
        ".mpeg", ".mpg", ".oga", ".ogg", ".ogv", ".opus", ".wav", ".webm", ".wma", ".wmv"
    };

    public bool IsSupported(string fileName, string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(mimeType)
            && (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                || mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return !UnsupportedExtensions.Contains(Path.GetExtension(fileName));
    }
}
