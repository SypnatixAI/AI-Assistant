using AssistantCore.Service.Application.Abstractions;

namespace AssistantCore.Service.Infrastructure.Authentication;

public sealed class HttpCorrelationIdProvider(IHttpContextAccessor httpContextAccessor)
    : ICorrelationIdProvider
{
    public string GetCorrelationId() =>
        httpContextAccessor.HttpContext?.TraceIdentifier
        ?? throw new InvalidOperationException("No HTTP request is currently active.");
}
