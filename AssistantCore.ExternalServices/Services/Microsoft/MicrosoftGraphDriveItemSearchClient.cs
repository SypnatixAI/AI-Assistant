using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftGraphDriveItemSearchClient(HttpClient httpClient)
{
    public async Task<IReadOnlyCollection<MicrosoftGraphDriveItemSearchResult>> SearchAsync(
        string graphBaseUrl,
        string accessToken,
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (!Uri.TryCreate(graphBaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Microsoft Graph base URL must use HTTPS.", nameof(graphBaseUrl));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(baseUri, "/v1.0/search/query"))
        {
            Content = JsonContent.Create(new
            {
                requests = new[]
                {
                    new
                    {
                        entityTypes = new[] { "driveItem" },
                        query = new { queryString = query },
                        from = 0,
                        size = 50,
                        fields = new[] { "id", "name", "webUrl", "parentReference", "file" }
                    }
                }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new MicrosoftExternalException(
                $"Microsoft Graph file search failed with status {(int)response.StatusCode}.",
                statusCode: response.StatusCode);
        }

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return ReadResults(document.RootElement);
    }

    private static IReadOnlyCollection<MicrosoftGraphDriveItemSearchResult> ReadResults(JsonElement root)
    {
        var results = new List<MicrosoftGraphDriveItemSearchResult>();
        if (!root.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var response in values.EnumerateArray())
        {
            if (!response.TryGetProperty("hitsContainers", out var containers)
                || containers.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var container in containers.EnumerateArray())
            {
                if (!container.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var hit in hits.EnumerateArray())
                {
                    if (!hit.TryGetProperty("resource", out var resource)
                        || resource.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var id = ReadString(resource, "id");
                    var name = ReadString(resource, "name");
                    var webUrl = ReadString(resource, "webUrl");
                    var driveId = resource.TryGetProperty("parentReference", out var parent)
                                  && parent.ValueKind == JsonValueKind.Object
                        ? ReadString(parent, "driveId")
                        : null;
                    var isFile = resource.TryGetProperty("file", out var file)
                                 && file.ValueKind == JsonValueKind.Object;

                    if (isFile
                        && !string.IsNullOrWhiteSpace(id)
                        && !string.IsNullOrWhiteSpace(name)
                        && !string.IsNullOrWhiteSpace(driveId))
                    {
                        results.Add(new MicrosoftGraphDriveItemSearchResult(
                            driveId,
                            id,
                            name,
                            webUrl));
                    }
                }
            }
        }

        return results;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
