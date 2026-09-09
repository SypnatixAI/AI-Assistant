using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AssistantCore.ExternalServices.Services.OpenAI;

internal sealed class OpenAiExternalChatClient(IChatClient innerClient) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await innerClient.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (ClientResultException exception)
        {
            throw new OpenAiExternalException(exception.Status, exception.Message);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerable<ChatResponseUpdate> updates;
        try
        {
            updates = innerClient.GetStreamingResponseAsync(messages, options, cancellationToken);
        }
        catch (ClientResultException exception)
        {
            throw new OpenAiExternalException(exception.Status, exception.Message);
        }

        await using var enumerator = updates.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (ClientResultException exception)
            {
                throw new OpenAiExternalException(exception.Status, exception.Message);
            }

            if (!hasNext)
            {
                yield break;
            }

            yield return enumerator.Current;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        innerClient.GetService(serviceType, serviceKey);

    public void Dispose() => innerClient.Dispose();
}
