using AssistantCore.Service.Application.Abstractions;

namespace AssistantCore.Service.Tests;

internal sealed class RecordingDispatcher : IDispatcher
{
    public required object Response { get; init; }

    public object? ReceivedRequest { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ReceivedRequest = request;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult((TResponse)Response);
    }
}
