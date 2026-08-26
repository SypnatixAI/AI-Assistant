using AssistantCore.ExternalServices.Services.OpenAI;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365EmbeddingGeneratorAdapter(
    OpenAiEmbeddingsClient client,
    IOptions<Microsoft365Options> options) : IMicrosoft365EmbeddingGenerator
{
    public async Task<IReadOnlyList<IReadOnlyList<float>>> CreateAsync(
        IReadOnlyCollection<string> contents,
        CancellationToken cancellationToken = default)
    {
        if (contents.Count == 0)
        {
            return [];
        }

        var configuration = options.Value;
        if (string.IsNullOrWhiteSpace(configuration.EmbeddingApiKey))
        {
            throw new InvalidOperationException("Microsoft365 embedding API key is required.");
        }

        var vectors = new List<IReadOnlyList<float>>(contents.Count);
        foreach (var batch in contents.Chunk(configuration.EmbeddingBatchSize))
        {
            vectors.AddRange(await client.CreateAsync(
                configuration.EmbeddingEndpoint,
                configuration.EmbeddingApiKey,
                configuration.EmbeddingModel,
                configuration.EmbeddingDimensions,
                batch,
                cancellationToken));
        }

        return vectors;
    }
}
