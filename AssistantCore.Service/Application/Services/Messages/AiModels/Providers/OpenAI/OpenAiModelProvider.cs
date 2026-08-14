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
        catch (OpenAiTransportException)
        {
            throw new AiProviderUnavailableException(ProviderName);
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
    {
        if (!string.Equals(
                request.Model.Provider,
                OpenAiProviderName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The selected model does not belong to the OpenAI provider.",
                nameof(request));
        }
    }
}
