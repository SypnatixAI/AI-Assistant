using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

internal sealed class AgentChatClientUsageTracker(
    IChatClient innerClient,
    ILogger<AgentChatClientUsageTracker>? logger = null) : IChatClient
{
    private int _modelCallCount;

    public int ModelCallCount => Volatile.Read(ref _modelCallCount);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var callNumber = Interlocked.Increment(ref _modelCallCount);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await innerClient.GetResponseAsync(messages, options, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
            logger?.LogInformation(
                "Agent model call #{ModelCallNumber} completed in {ElapsedMilliseconds} ms.",
                callNumber,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var callNumber = Interlocked.Increment(ref _modelCallCount);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await foreach (var update in innerClient.GetStreamingResponseAsync(
                               messages,
                               options,
                               cancellationToken))
            {
                yield return update;
            }
        }
        finally
        {
            stopwatch.Stop();
            logger?.LogInformation(
                "Agent model call #{ModelCallNumber} completed in {ElapsedMilliseconds} ms.",
                callNumber,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        innerClient.GetService(serviceType, serviceKey);

    public void Dispose() => innerClient.Dispose();
}
