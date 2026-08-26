using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AssistantCore.ExternalServices.Services.OpenAI;

public sealed class OpenAiEmbeddingsClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<IReadOnlyList<float>>> CreateAsync(
        string endpoint,
        string apiKey,
        string model,
        int dimensions,
        IReadOnlyCollection<string> inputs,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{endpoint.TrimEnd('/')}/embeddings")
        {
            Content = JsonContent.Create(new
            {
                model,
                input = inputs,
                dimensions
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new OpenAiExternalException((int)response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken)
            ?? throw new OpenAiExternalException(-1);
        var vectors = payload.Data.OrderBy(item => item.Index).Select(item => item.Embedding).ToArray();
        if (vectors.Length != inputs.Count
            || vectors.Any(vector => vector.Count != dimensions
                || vector.Any(value => float.IsNaN(value) || float.IsInfinity(value))))
        {
            throw new OpenAiExternalException(-1);
        }

        return vectors;
    }

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("data")] IReadOnlyCollection<EmbeddingItem> Data);

    private sealed record EmbeddingItem(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] IReadOnlyList<float> Embedding);
}
