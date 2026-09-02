using System.Text.Json;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;

namespace AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;

public sealed class OpenAiModelProvider(
    IOpenAiResponsesClient responsesClient,
    OpenAiResponseMapper responseMapper,
    TimeSpan timeout) : IAiModelProvider
{
    public const string OpenAiProviderName = "OpenAI";

    public string ProviderName => OpenAiProviderName;

    public async Task<AiModelResponse> GetNextActionAsync(
        AiModelRequest request,
        CancellationToken cancellationToken)
    {
        EnsureRequestUsesOpenAi(request);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            var response = await responsesClient.CreateResponseAsync(
                request,
                timeoutSource.Token);

            return responseMapper.Map(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new AiProviderTimeoutException(ProviderName);
        }
        catch (OpenAiTransportException exception) when (exception.StatusCode is 408 or 504)
        {
            throw new AiProviderTimeoutException(ProviderName);
        }
        catch (OpenAiTransportException exception) when (exception.StatusCode is 429)
        {
            throw new AiProviderLimitException(ProviderName);
        }
        catch (OpenAiTransportException exception)
        {
            throw new AiProviderUnavailableException(
                ProviderName,
                exception.StatusCode);
        }
        catch (AiProviderException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException
                or FormatException
                or InvalidOperationException
                or NotSupportedException)
        {
            throw new AiProviderInvalidResponseException(ProviderName);
        }
    }

    public async Task<AiModelResponse> GetNextActionStreamingAsync(
        AiModelRequest request,
        Func<string, CancellationToken, ValueTask> onTextDelta,
        CancellationToken cancellationToken)
    {
        EnsureRequestUsesOpenAi(request);
        ArgumentNullException.ThrowIfNull(onTextDelta);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            var response = await responsesClient.CreateResponseStreamingAsync(
                request,
                onTextDelta,
                timeoutSource.Token);

            return responseMapper.Map(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new AiProviderTimeoutException(ProviderName);
        }
        catch (OpenAiTransportException exception) when (exception.StatusCode is 408 or 504)
        {
            throw new AiProviderTimeoutException(ProviderName);
        }
        catch (OpenAiTransportException exception) when (exception.StatusCode is 429)
        {
            throw new AiProviderLimitException(ProviderName);
        }
        catch (OpenAiTransportException exception)
        {
            throw new AiProviderUnavailableException(ProviderName, exception.StatusCode);
        }
        catch (AiProviderException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException
                or FormatException
                or InvalidOperationException
                or NotSupportedException)
        {
            throw new AiProviderInvalidResponseException(ProviderName);
        }
    }

    public async Task<string> CreateConversationSummaryAsync(
        AiConversationSummaryRequest request,
        CancellationToken cancellationToken)
    {
        EnsureRequestUsesOpenAi(request.Model);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            var summary = await responsesClient.CreateConversationSummaryAsync(
                request,
                timeoutSource.Token);

            if (string.IsNullOrWhiteSpace(summary))
            {
                throw new AiProviderInvalidResponseException(ProviderName);
            }

            return summary.Trim();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new AiProviderTimeoutException(ProviderName);
        }
        catch (OpenAiTransportException exception) when (exception.StatusCode is 408 or 504)
        {
            throw new AiProviderTimeoutException(ProviderName);
        }
        catch (OpenAiTransportException exception) when (exception.StatusCode is 429)
        {
            throw new AiProviderLimitException(ProviderName);
        }
        catch (OpenAiTransportException exception)
        {
            throw new AiProviderUnavailableException(ProviderName, exception.StatusCode);
        }
        catch (AiProviderException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException
                or FormatException
                or InvalidOperationException
                or NotSupportedException)
        {
            throw new AiProviderInvalidResponseException(ProviderName);
        }
    }

    private static void EnsureRequestUsesOpenAi(AiModelRequest request)
        => EnsureRequestUsesOpenAi(request.Model);

    private static void EnsureRequestUsesOpenAi(SelectedAiModel model)
    {
        if (!string.Equals(
                model.Provider,
                OpenAiProviderName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The selected model does not belong to the OpenAI provider.",
                nameof(model));
        }
    }
}
