using System.Net;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftExternalException : Exception
{
    public MicrosoftExternalException(
        string message,
        Exception? innerException = null,
        HttpStatusCode? statusCode = null,
        TimeSpan? retryAfterDelay = null,
        DateTimeOffset? retryAfterAt = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        RetryAfterDelay = retryAfterDelay;
        RetryAfterAt = retryAfterAt;
    }

    public HttpStatusCode? StatusCode { get; }

    public TimeSpan? RetryAfterDelay { get; }

    public DateTimeOffset? RetryAfterAt { get; }
}
