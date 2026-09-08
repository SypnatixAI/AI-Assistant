using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

internal sealed class AgentChatClientUsageTracker(IChatClient innerClient) : IChatClient
{
    private int _modelCallCount;

    public int ModelCallCount => Volatile.Read(ref _modelCallCount);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _modelCallCount);
        return await innerClient.GetResponseAsync(messages, options, cancellationToken);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _modelCallCount);

        await foreach (var update in innerClient.GetStreamingResponseAsync(
                           messages,
                           options,
                           cancellationToken))
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        innerClient.GetService(serviceType, serviceKey);

    public void Dispose() => innerClient.Dispose();
}
