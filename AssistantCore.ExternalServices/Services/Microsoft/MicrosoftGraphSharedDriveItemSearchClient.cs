using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

/// <summary>
/// Discovers content accessible from a user's drive through the stable Graph
/// drive search API. Results stored outside the user's own drive expose the
/// remoteItem facet, which is normalized to the real owner drive/item identity.
///
/// This intentionally does not use /sharedWithMe, which is deprecated.
/// </summary>
public sealed class MicrosoftGraphSharedDriveItemSearchClient(HttpClient httpClient)
{
    private readonly MicrosoftGraphCollectionReader collectionReader = new(httpClient);

    public async IAsyncEnumerable<MicrosoftDriveItemDelta> GetSharedItemsAsync(
        string graphBaseUrl,
        string accessToken,
        string userDriveId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var firstPageUri = CreateSearchUri(graphBaseUrl, userDriveId);
        await foreach (var page in collectionReader.ReadPagesAsync<SearchDriveItem, MicrosoftDriveItemDelta?>(
                           firstPageUri,
                           accessToken,
                           MapSharedItem,
                           "shared drive item search",
                           cancellationToken))
        {
            foreach (var item in page.Items)
            {
                if (item is not null)
                {
                    yield return item;
                }
            }
        }
    }

    private static Uri CreateSearchUri(string graphBaseUrl, string driveId)
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
        var escapedDriveId = Uri.EscapeDataString(driveId);
        return new Uri(
            normalizedBaseUri,
            $"v1.0/drives/{escapedDriveId}/search(q='')?$select=id,name,eTag,createdDateTime,lastModifiedDateTime,webUrl,size,file,folder,remoteItem,parentReference&$top=200");
    }

    private static MicrosoftDriveItemDelta? MapSharedItem(SearchDriveItem item)
    {
        var remote = item.RemoteItem;
        var canonicalDriveId = remote?.ParentReference?.DriveId;
        var canonicalItemId = remote?.Id;

        // Search also returns ordinary items from the user's own drive. Delta
        // already owns those, so only remoteItem results are added here.
        if (string.IsNullOrWhiteSpace(canonicalDriveId)
            || string.IsNullOrWhiteSpace(canonicalItemId))
        {
            return null;
        }

        var name = remote?.Name ?? item.Name;
        var eTag = remote?.ETag ?? item.ETag;
        var fileFacet = remote?.File ?? item.File;
        var folderFacet = remote?.Folder ?? item.Folder;
        var isFolder = HasFacet(folderFacet);
        var isFile = fileFacet is { ValueKind: JsonValueKind.Object };

        if (!isFolder && !isFile)
        {
            return null;
        }

        if (isFile && (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(eTag)))
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph shared drive item search returned an incomplete file.");
        }

        return new MicrosoftDriveItemDelta(
            canonicalItemId,
            name,
            eTag,
            remote?.CreatedDateTime ?? item.CreatedDateTime,
            remote?.LastModifiedDateTime ?? item.LastModifiedDateTime,
            remote?.WebUrl ?? item.WebUrl,
            remote?.Size ?? item.Size,
            ReadMimeType(fileFacet),
            IsDeleted: false,
            IsFolder: isFolder,
            IsFile: isFile)
        {
            CanonicalDriveId = canonicalDriveId
        };
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

    private sealed record SearchDriveItem(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("eTag")] string? ETag,
        [property: JsonPropertyName("createdDateTime")] DateTimeOffset? CreatedDateTime,
        [property: JsonPropertyName("lastModifiedDateTime")] DateTimeOffset? LastModifiedDateTime,
        [property: JsonPropertyName("webUrl")] string? WebUrl,
        [property: JsonPropertyName("size")] long? Size,
        [property: JsonPropertyName("file")] JsonElement? File,
        [property: JsonPropertyName("folder")] JsonElement? Folder,
        [property: JsonPropertyName("parentReference")] ParentReference? ParentReference,
        [property: JsonPropertyName("remoteItem")] RemoteDriveItem? RemoteItem);

    private sealed record RemoteDriveItem(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("eTag")] string? ETag,
        [property: JsonPropertyName("createdDateTime")] DateTimeOffset? CreatedDateTime,
        [property: JsonPropertyName("lastModifiedDateTime")] DateTimeOffset? LastModifiedDateTime,
        [property: JsonPropertyName("webUrl")] string? WebUrl,
        [property: JsonPropertyName("size")] long? Size,
        [property: JsonPropertyName("file")] JsonElement? File,
        [property: JsonPropertyName("folder")] JsonElement? Folder,
        [property: JsonPropertyName("parentReference")] ParentReference? ParentReference);

    private sealed record ParentReference(
        [property: JsonPropertyName("driveId")] string? DriveId);
}
