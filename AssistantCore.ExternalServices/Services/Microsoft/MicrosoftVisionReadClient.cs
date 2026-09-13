using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftVisionReadClient(HttpClient httpClient)
{
    private const string ApiVersion = "3.2";

    public async Task<MicrosoftOcrResult> ReadAsync(
        string endpoint,
        string apiKey,
        Stream content,
        string mediaType,
        int timeoutSeconds,
        int pollIntervalMilliseconds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentNullException.ThrowIfNull(content);
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            || endpointUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("An HTTPS OCR endpoint is required.", nameof(endpoint));
        }
        if (timeoutSeconds <= 0 || pollIntervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(endpointUri, $"/vision/v{ApiVersion}/read/analyze"));
        request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType)
        {
            return new MicrosoftOcrResult(MicrosoftOcrStatus.NoIndexableContent, []);
        }
        if (!response.IsSuccessStatusCode
            || !response.Headers.TryGetValues("Operation-Location", out var locations))
        {
            return new MicrosoftOcrResult(MicrosoftOcrStatus.Unavailable, []);
        }

        var operationUri = new Uri(locations.Single(), UriKind.Absolute);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            while (true)
            {
                await Task.Delay(pollIntervalMilliseconds, timeout.Token);
                using var poll = new HttpRequestMessage(HttpMethod.Get, operationUri);
                poll.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
                using var pollResponse = await httpClient.SendAsync(poll, timeout.Token);
                if (!pollResponse.IsSuccessStatusCode)
                {
                    return new MicrosoftOcrResult(MicrosoftOcrStatus.Unavailable, []);
                }

                var result = await pollResponse.Content.ReadFromJsonAsync<ReadOperation>(
                    cancellationToken: timeout.Token);
                if (string.Equals(result?.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    var readResults = result?.AnalyzeResult?.ReadResults;
                    return new MicrosoftOcrResult(
                        MicrosoftOcrStatus.Success,
                        readResults?
                            .Select(page => new MicrosoftOcrPage(
                                page.Page,
                                Normalize(string.Join(
                                    " ",
                                    page.Lines is { } lines
                                        ? lines.Select(line => line.Text ?? string.Empty)
                                        : []))))
                            .Where(page => page.Text.Length > 0)
                            .ToArray() ?? []);
                }
                if (string.Equals(result?.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    return new MicrosoftOcrResult(MicrosoftOcrStatus.NoIndexableContent, []);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new MicrosoftOcrResult(MicrosoftOcrStatus.Timeout, []);
        }
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed record ReadOperation(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("analyzeResult")] AnalyzeResult? AnalyzeResult);

    private sealed record AnalyzeResult(
        [property: JsonPropertyName("readResults")] IReadOnlyList<ReadPage>? ReadResults);

    private sealed record ReadPage(
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("lines")] IReadOnlyList<ReadLine>? Lines);

    private sealed record ReadLine([property: JsonPropertyName("text")] string? Text);
}
