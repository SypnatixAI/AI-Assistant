namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftOcrResult(
    MicrosoftOcrStatus Status,
    IReadOnlyList<MicrosoftOcrPage> Pages);

public sealed record MicrosoftOcrPage(int PageNumber, string Text);

public enum MicrosoftOcrStatus
{
    Success,
    NoIndexableContent,
    TooLarge,
    Timeout,
    Unavailable
}
