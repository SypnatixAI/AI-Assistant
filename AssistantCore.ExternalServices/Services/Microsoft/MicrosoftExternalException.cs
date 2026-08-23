using System.Net;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftExternalException : Exception
{
    public MicrosoftExternalException(
        string message,
        Exception? innerException = null,
        HttpStatusCode? statusCode = null,
        string? errorCode = null,
        TimeSpan? retryAfterDelay = null,
        DateTimeOffset? retryAfterAt = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        RetryAfterDelay = retryAfterDelay;
        RetryAfterAt = retryAfterAt;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? ErrorCode { get; }

    public TimeSpan? RetryAfterDelay { get; }

    public DateTimeOffset? RetryAfterAt { get; }
}
