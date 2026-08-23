using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftGraphListItemDeltaClient(HttpClient httpClient)
{
    private readonly MicrosoftGraphCollectionReader collectionReader = new(httpClient);

    public async IAsyncEnumerable<MicrosoftListItemDeltaPage> GetInitialPagesAsync(
        string graphBaseUrl,
        string accessToken,
        string siteId,
        string listId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var page in GetPagesAsync(
                           CreateInitialDeltaUri(graphBaseUrl, siteId, listId),
                           accessToken,
                           cancellationToken))
        {
            yield return page;
        }
    }

    public async IAsyncEnumerable<MicrosoftListItemDeltaPage> GetDeltaPagesAsync(
        string graphBaseUrl,
        string accessToken,
        string deltaLink,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var page in GetPagesAsync(
                           CreateStoredDeltaUri(graphBaseUrl, deltaLink),
                           accessToken,
                           cancellationToken))
        {
            yield return page;
        }
    }

    private async IAsyncEnumerable<MicrosoftListItemDeltaPage> GetPagesAsync(
        Uri firstPageUri,
        string accessToken,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var foundFinalDeltaLink = false;
        await foreach (var page in collectionReader.ReadPagesAsync<ListItem, MicrosoftListItemDelta>(
                           firstPageUri,
                           accessToken,
                           MapItem,
                           "list item delta",
                           cancellationToken))
        {
            if (page.DeltaLink is not null)
            {
                foundFinalDeltaLink = true;
            }

            yield return new MicrosoftListItemDeltaPage(page.Items, page.DeltaLink);
        }

        if (!foundFinalDeltaLink)
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph list item delta response did not contain a final delta link.");
        }
    }

    private static Uri CreateStoredDeltaUri(string graphBaseUrl, string deltaLink)
    {
        if (!Uri.TryCreate(graphBaseUrl, UriKind.Absolute, out var graphBaseUri)
            || graphBaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Microsoft Graph base URL must use HTTPS.", nameof(graphBaseUrl));
        }

        if (!Uri.TryCreate(deltaLink, UriKind.Absolute, out var deltaUri)
            || deltaUri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(deltaUri.Host, graphBaseUri.Host, StringComparison.OrdinalIgnoreCase)
            || deltaUri.Port != graphBaseUri.Port)
        {
            throw new ArgumentException(
                "Microsoft Graph delta link must be an HTTPS URL from the configured Graph authority.",
                nameof(deltaLink));
        }

        return deltaUri;
    }

    private static Uri CreateInitialDeltaUri(
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
            $"v1.0/sites/{Uri.EscapeDataString(siteId)}/lists/{Uri.EscapeDataString(listId)}/items/delta?$expand=fields");
    }

    private static MicrosoftListItemDelta MapItem(ListItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph list item delta response contained an invalid item.");
        }

        var isDeleted = HasFacet(item.Deleted);
        if (!isDeleted
            && (string.IsNullOrWhiteSpace(item.ETag)
                || item.Fields is not { ValueKind: JsonValueKind.Object }))
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph list item delta response contained an incomplete active item.");
        }

        return new MicrosoftListItemDelta(
            item.Id,
            isDeleted ? null : item.ETag,
            item.CreatedDateTime,
            item.LastModifiedDateTime,
            item.WebUrl,
            isDeleted ? null : item.Fields?.Clone(),
            isDeleted);
    }

    private static bool HasFacet(JsonElement? facet) =>
        facet is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
        && facet.Value.ValueKind != JsonValueKind.False;

    private sealed record ListItem(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("eTag")] string? ETag,
        [property: JsonPropertyName("createdDateTime")] DateTimeOffset? CreatedDateTime,
        [property: JsonPropertyName("lastModifiedDateTime")] DateTimeOffset? LastModifiedDateTime,
        [property: JsonPropertyName("webUrl")] string? WebUrl,
        [property: JsonPropertyName("fields")] JsonElement? Fields,
        [property: JsonPropertyName("deleted")] JsonElement? Deleted);
}
