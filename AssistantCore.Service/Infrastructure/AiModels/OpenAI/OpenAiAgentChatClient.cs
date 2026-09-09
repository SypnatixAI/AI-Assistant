using System.Runtime.CompilerServices;
using AssistantCore.ExternalServices.Services.OpenAI;
using AssistantCore.Service.Application.Exceptions;
using Microsoft.Extensions.AI;

namespace AssistantCore.Service.Infrastructure.AiModels.OpenAI;

internal sealed class OpenAiAgentChatClient(
    IChatClient innerClient,
    TimeSpan timeout) : IChatClient
{
    private const string ProviderName = "OpenAI";

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CreateTimeoutSource(cancellationToken);

        try
        {
            return await innerClient.GetResponseAsync(
                messages,
                options,
                timeoutSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new AiProviderTimeoutException(ProviderName);
        }
        catch (OpenAiExternalException exception)
        {
            throw MapExternalException(exception);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CreateTimeoutSource(cancellationToken);

        IAsyncEnumerable<ChatResponseUpdate> updates;
        try
        {
            updates = innerClient.GetStreamingResponseAsync(
                messages,
                options,
                timeoutSource.Token);
        }
        catch (OpenAiExternalException exception)
        {
            throw MapExternalException(exception);
        }

        await using var enumerator = updates.GetAsyncEnumerator(timeoutSource.Token);
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw new AiProviderTimeoutException(ProviderName);
            }
            catch (OpenAiExternalException exception)
            {
                throw MapExternalException(exception);
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

    private CancellationTokenSource CreateTimeoutSource(CancellationToken cancellationToken)
    {
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        return timeoutSource;
    }

    private static Exception MapExternalException(OpenAiExternalException exception) =>
        exception.StatusCode switch
        {
            408 or 504 => new AiProviderTimeoutException(ProviderName),
            429 => new AiProviderLimitException(ProviderName),
            _ => new AiProviderUnavailableException(
                ProviderName,
                exception.StatusCode,
                exception.ProviderErrorMessage)
        };
}
