using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftGraphDriveItemDeltaClient(HttpClient httpClient)
{
    private static readonly string[] PermissionTrackingPreferences =
    [
        "hierarchicalsharing",
        "deltashowremovedasdeleted",
        "deltatraversepermissiongaps",
        "deltashowsharingchanges"
    ];
    private readonly MicrosoftGraphCollectionReader collectionReader = new(httpClient);

    public async IAsyncEnumerable<MicrosoftDriveItemDeltaPage> GetInitialPagesAsync(
        string graphBaseUrl,
        string accessToken,
        string driveId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var page in GetPagesAsync(
                           CreateInitialDeltaUri(graphBaseUrl, driveId),
                           accessToken,
                           cancellationToken))
        {
            yield return page;
        }
    }

    public async IAsyncEnumerable<MicrosoftDriveItemDeltaPage> GetDeltaPagesAsync(
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

    private async IAsyncEnumerable<MicrosoftDriveItemDeltaPage> GetPagesAsync(
        Uri firstPageUri,
        string accessToken,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var foundFinalDeltaLink = false;
        await foreach (var page in collectionReader.ReadPagesAsync<DriveItem, MicrosoftDriveItemDelta>(
                           firstPageUri,
                           accessToken,
                           MapItem,
                           "drive item delta",
                           cancellationToken,
                           PermissionTrackingPreferences))
        {
            foundFinalDeltaLink |= page.DeltaLink is not null;
            yield return new MicrosoftDriveItemDeltaPage(page.Items, page.DeltaLink);
        }

        if (!foundFinalDeltaLink)
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph drive item delta response did not contain a final delta link.");
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

    private static Uri CreateInitialDeltaUri(string graphBaseUrl, string driveId)
    {
        if (string.IsNullOrWhiteSpace(driveId))
        {
            throw new ArgumentException("Microsoft Graph drive identifier is required.", nameof(driveId));
        }

        if (!Uri.TryCreate(graphBaseUrl, UriKind.Absolute, out var graphBaseUri)
            || graphBaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Microsoft Graph base URL must use HTTPS.", nameof(graphBaseUrl));
        }

        var normalizedBaseUri = new Uri($"{graphBaseUri.GetLeftPart(UriPartial.Authority)}/");
        return new Uri(
            normalizedBaseUri,
            $"v1.0/drives/{Uri.EscapeDataString(driveId)}/root/delta");
    }

    private static MicrosoftDriveItemDelta MapItem(DriveItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph drive item delta response contained an invalid item.");
        }

        var isDeleted = HasFacet(item.Deleted);
        var isFolder = HasFacet(item.Folder);
        var isFile = item.File is { ValueKind: JsonValueKind.Object };
        if (!isDeleted && !isFolder && !isFile)
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph drive item delta response contained an unsupported item facet.");
        }

        if (isFile && !isDeleted
            && (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.ETag)))
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph drive item delta response contained an incomplete active file.");
        }

        return new MicrosoftDriveItemDelta(
            item.Id,
            isDeleted ? null : item.Name,
            isDeleted ? null : item.ETag,
            item.CreatedDateTime,
            item.LastModifiedDateTime,
            item.WebUrl,
            item.Size,
            isDeleted ? null : ReadMimeType(item.File),
            isDeleted,
            isFolder,
            isFile);
    }

    private static string? ReadMimeType(JsonElement? fileFacet) =>
        fileFacet is { ValueKind: JsonValueKind.Object } file
        && file.TryGetProperty("mimeType", out var mimeType)
        && mimeType.ValueKind == JsonValueKind.String
            ? mimeType.GetString()
            : null;

    private static bool HasFacet(JsonElement? facet) =>
        facet is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
        && facet.Value.ValueKind != JsonValueKind.False;

    private sealed record DriveItem(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("eTag")] string? ETag,
        [property: JsonPropertyName("createdDateTime")] DateTimeOffset? CreatedDateTime,
        [property: JsonPropertyName("lastModifiedDateTime")] DateTimeOffset? LastModifiedDateTime,
        [property: JsonPropertyName("webUrl")] string? WebUrl,
        [property: JsonPropertyName("size")] long? Size,
        [property: JsonPropertyName("file")] JsonElement? File,
        [property: JsonPropertyName("folder")] JsonElement? Folder,
        [property: JsonPropertyName("deleted")] JsonElement? Deleted);
}
