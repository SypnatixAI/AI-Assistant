namespace AssistantCore.Service.Application.Services.Microsoft365;

public static class Microsoft365DocumentIndexVersion
{
    private const int CurrentPipelineVersion = 2;

    public static string Create(string documentETag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentETag);

        return $"{CurrentPipelineVersion}:{documentETag}";
    }
}
