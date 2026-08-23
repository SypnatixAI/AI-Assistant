using System.Text.Json;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftGraphListSchemaClient(HttpClient httpClient)
{
    private readonly MicrosoftGraphCollectionReader collectionReader = new(httpClient);

    public Task<IReadOnlyCollection<MicrosoftListColumn>> GetColumnsAsync(
        string graphBaseUrl,
        string accessToken,
        string siteId,
        string listId,
        CancellationToken cancellationToken = default) =>
        collectionReader.ReadAsync<JsonElement, MicrosoftListColumn>(
            CreateColumnsUri(graphBaseUrl, siteId, listId),
            accessToken,
            MapColumn,
            "list columns",
            cancellationToken);

    private static Uri CreateColumnsUri(
        string graphBaseUrl,
        string siteId,
        string listId)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            throw new ArgumentException("Microsoft Graph site identifier is required.", nameof(siteId));
        }

        if (string.IsNullOrWhiteSpace(listId))
        {
            throw new ArgumentException("Microsoft Graph list identifier is required.", nameof(listId));
        }

        if (!Uri.TryCreate(graphBaseUrl, UriKind.Absolute, out var graphBaseUri)
            || graphBaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Microsoft Graph base URL must use HTTPS.", nameof(graphBaseUrl));
        }

        var normalizedBaseUri = new Uri($"{graphBaseUri.GetLeftPart(UriPartial.Authority)}/");
        return new Uri(
            normalizedBaseUri,
            $"v1.0/sites/{Uri.EscapeDataString(siteId)}/lists/{Uri.EscapeDataString(listId)}/columns");
    }

    private static MicrosoftListColumn MapColumn(JsonElement column)
    {
        if (column.ValueKind != JsonValueKind.Object
            || !column.TryGetProperty("id", out var idProperty)
            || string.IsNullOrWhiteSpace(idProperty.GetString()))
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph list columns response contained an invalid column.");
        }

        return new MicrosoftListColumn(idProperty.GetString()!, column.Clone());
    }
}
