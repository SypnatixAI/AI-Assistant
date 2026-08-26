using AssistantCore.ExternalServices.Services.Azure;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365SearchIndexInitializerAdapter(
    AzureAiSearchIndexClient client,
    IOptions<AzureAiSearchOptions> searchOptions,
    IOptions<Microsoft365Options> microsoft365Options) : IMicrosoft365SearchIndexInitializer
{
    public Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        var search = searchOptions.Value;
        return search.EnsureIndexOnStartup
            ? client.EnsureCreatedAsync(
                search.Endpoint,
                search.IndexName,
                search.ApiKey,
                microsoft365Options.Value.EmbeddingDimensions,
                cancellationToken)
            : Task.CompletedTask;
    }
}
